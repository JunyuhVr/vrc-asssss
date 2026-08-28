using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class ChromixUniClueGame : UdonSharpBehaviour
{
    private const int MaxPlayers = 6;
    private const int PhaseLobby = 0;
    private const int PhasePick = 5;
    private const int PhaseClues = 1;
    private const int PhaseReveal = 3;
    private const int PhaseGameOver = 4;
    private const int ModeClassic = 1;
    private const int ModeParty = 2;

    [UdonSynced] private int[] _playerIds = new int[MaxPlayers];
    [UdonSynced] private string[] _playerNames = new string[MaxPlayers];
    [UdonSynced] private string[] _clues = new string[MaxPlayers];
    [UdonSynced] private int[] _clueStates = new int[MaxPlayers];
    [UdonSynced] private int _hostId = -1;
    [UdonSynced] private int _phase = PhaseLobby;
    [UdonSynced] private int _mode = ModeClassic;
    [UdonSynced] private int _round = 0;
    [UdonSynced] private int _maxRounds = 8;
    [UdonSynced] private int _pickerSlot = -1;
    [UdonSynced] private int _wordIndex = 0;
    [UdonSynced] private int[] _wordChoices = new int[3];
    [UdonSynced] private int _teamScore = 0;
    [UdonSynced] private string _lastGuess = "";
    [UdonSynced] private int _lastResult = 0;
    [UdonSynced] private double _phaseDeadline = 0d;

    private GameObject _lobbyView;
    private GameObject _gameView;
    private GameObject _tutorial;
    private GameObject _clueEntry;
    private GameObject _guessEntry;
    private GameObject _clueBoard;
    private GameObject _wordPickView;
    private RectTransform _stage;
    private Text _statusText;
    private Text _modeText;
    private Text _roundText;
    private Text _scoreText;
    private Text _wordText;
    private Text _timerText;
    private Text _feedbackText;
    private Text[] _seatNames = new Text[MaxPlayers];
    private Text[] _seatRoles = new Text[MaxPlayers];
    private GameObject[] _seatPlaceholders = new GameObject[MaxPlayers];
    private Image[] _seatFrames = new Image[MaxPlayers];
    private Text[] _clueTexts = new Text[MaxPlayers];
    private Camera[] _portraitCameras = new Camera[MaxPlayers];
    private InputField _clueInput;
    private InputField _guessInput;
    private Button _classicButton;
    private Button _partyButton;
    private Button _leaveButton;
    private Button _resetButton;
    private Button[] _wordPickButtons = new Button[3];

    private string[] _words;
    private int _localSlot = -1;
    private bool _uiReady;
    private bool _tutorialOpen;
    private bool[] _portraitDirty = new bool[MaxPlayers];
    private int[] _lastSnapshotIds = new int[MaxPlayers];
    private int _portraitRenderingSlot = -1;
    private float _entrance;
    private float _pulse;

    private void Start()
    {
        LoadWords();
        for (int i = 0; i < MaxPlayers; i++)
        {
            if (_playerNames[i] == null) _playerNames[i] = "";
            if (_clues[i] == null) _clues[i] = "";
            _lastSnapshotIds[i] = -1;
        }
        for (int i = 0; i < 3; i++) _wordChoices[i] = 0;
        FindUi();
        RefreshLocalSlot();
        ApplyState(true);
    }

    private void Update()
    {
        if (!_uiReady) return;
        UpdateAnimation();
        UpdatePortraits();
        UpdateTimer();
        if (!Networking.IsOwner(gameObject) || _phaseDeadline <= 0d) return;
        if (Networking.GetServerTimeInSeconds() < _phaseDeadline) return;
        if (_phase == PhasePick)
        {
            _wordIndex = _wordChoices[Random.Range(0, 3)];
            BeginClues();
        }
        else if (_phase == PhaseClues)
        {
            ResolveRound(0, "SKIPPED");
        }
    }

    public override void OnDeserialization()
    {
        RefreshLocalSlot();
        ApplyState(true);
    }

    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        if (!Networking.IsOwner(gameObject)) return;
        int slot = FindSlot(player.playerId);
        if (slot < 0) return;
        ClearSeat(slot);
        if (_hostId == player.playerId) _hostId = FirstPlayerId();
        if (_phase != PhaseLobby && (slot == _pickerSlot || PlayerCount() < 2))
        {
            ResetState();
        }
        RequestSerialization();
        ApplyState(true);
    }

    private void FindUi()
    {
        Transform asset = transform.parent;
        if (asset == null) return;
        Transform canvas = asset.Find("UI_Canvas");
        Transform portraits = asset.Find("PortraitRig");
        if (canvas == null || portraits == null) return;
        _stage = FindRectTransform(canvas, "Stage");
        _lobbyView = FindGameObject(canvas, "Stage/MainPanel/LobbyView");
        _gameView = FindGameObject(canvas, "Stage/MainPanel/GameView");
        _tutorial = FindGameObject(canvas, "Stage/Tutorial");
        _wordPickView = FindGameObject(canvas, "Stage/MainPanel/GameView/WordPickView");
        _clueEntry = FindGameObject(canvas, "Stage/MainPanel/GameView/ClueEntry");
        _guessEntry = FindGameObject(canvas, "Stage/MainPanel/GameView/GuessEntry");
        _clueBoard = FindGameObject(canvas, "Stage/MainPanel/GameView/ClueBoard");
        _statusText = FindText(canvas, "Stage/MainPanel/StatusText");
        _modeText = FindText(canvas, "Stage/MainPanel/ModeText");
        _roundText = FindText(canvas, "Stage/MainPanel/GameView/RoundText");
        _scoreText = FindText(canvas, "Stage/MainPanel/GameView/ScoreText");
        _wordText = FindText(canvas, "Stage/MainPanel/GameView/WordCard/WordText");
        _timerText = FindText(canvas, "Stage/MainPanel/GameView/TimerText");
        _feedbackText = FindText(canvas, "Stage/MainPanel/GameView/FeedbackText");
        _clueInput = FindInputField(canvas, "Stage/MainPanel/GameView/ClueEntry/ClueInput");
        _guessInput = FindInputField(canvas, "Stage/MainPanel/GameView/GuessEntry/GuessInput");
        _classicButton = FindButton(canvas, "Stage/MainPanel/LobbyView/ClassicButton");
        _partyButton = FindButton(canvas, "Stage/MainPanel/LobbyView/PartyButton");
        _leaveButton = FindButton(canvas, "Stage/MainPanel/LeaveButton");
        _resetButton = FindButton(canvas, "Stage/MainPanel/GameView/ResetButton");
        for (int i = 0; i < 3; i++)
        {
            _wordPickButtons[i] = FindButton(canvas, "Stage/MainPanel/GameView/WordPickView/WordChoice" + i);
        }
        for (int i = 0; i < MaxPlayers; i++)
        {
            Transform seat = canvas.Find("Stage/Seats/Seat" + i);
            if (seat != null)
            {
                _seatFrames[i] = seat.GetComponent<Image>();
                _seatNames[i] = FindText(seat, "NameText");
                _seatRoles[i] = FindText(seat, "RoleText");
                Transform placeholder = seat.Find("Portrait/Placeholder");
                if (placeholder != null) _seatPlaceholders[i] = placeholder.gameObject;
            }
            _clueTexts[i] = FindText(canvas, "Stage/MainPanel/GameView/ClueBoard/ClueCard" + i + "/Clue" + i);
            Transform cameraTransform = portraits.Find("PortraitCamera" + i);
            if (cameraTransform != null) _portraitCameras[i] = cameraTransform.GetComponent<Camera>();
        }
        if (_tutorial != null) _tutorial.SetActive(false);
        _uiReady = true;
    }

    private GameObject FindGameObject(Transform root, string path)
    {
        Transform t = root.Find(path);
        return t != null ? t.gameObject : null;
    }

    private RectTransform FindRectTransform(Transform root, string path)
    {
        Transform t = root.Find(path);
        return t != null ? t.GetComponent<RectTransform>() : null;
    }

    private Text FindText(Transform root, string path)
    {
        Transform t = root.Find(path);
        return t != null ? t.GetComponent<Text>() : null;
    }

    private Button FindButton(Transform root, string path)
    {
        Transform t = root.Find(path);
        return t != null ? t.GetComponent<Button>() : null;
    }

    private InputField FindInputField(Transform root, string path)
    {
        Transform t = root.Find(path);
        return t != null ? t.GetComponent<InputField>() : null;
    }

    private void RefreshLocalSlot()
    {
        VRCPlayerApi local = Networking.LocalPlayer;
        _localSlot = local == null ? -1 : FindSlot(local.playerId);
    }

    private int FindSlot(int playerId)
    {
        for (int i = 0; i < MaxPlayers; i++)
        {
            if (_playerIds[i] == playerId) return i;
        }
        return -1;
    }

    private int FirstOpenSlot()
    {
        for (int i = 0; i < MaxPlayers; i++)
        {
            if (_playerIds[i] <= 0) return i;
        }
        return -1;
    }

    private int FirstPlayerId()
    {
        for (int i = 0; i < MaxPlayers; i++)
        {
            if (_playerIds[i] > 0) return _playerIds[i];
        }
        return -1;
    }

    private int PlayerCount()
    {
        int count = 0;
        for (int i = 0; i < MaxPlayers; i++)
        {
            if (_playerIds[i] > 0) count++;
        }
        return count;
    }

    private int NextOccupiedSlot(int after)
    {
        for (int step = 1; step <= MaxPlayers; step++)
        {
            int slot = (after + step) % MaxPlayers;
            if (_playerIds[slot] > 0) return slot;
        }
        return -1;
    }

    private void ClearSeat(int slot)
    {
        _playerIds[slot] = -1;
        _playerNames[slot] = "";
        _clues[slot] = "";
        _clueStates[slot] = 0;
    }

    private bool IsHost(VRCPlayerApi player)
    {
        return player != null && player.playerId == _hostId;
    }

    private void DetectPortraitChanges()
    {
        for (int i = 0; i < MaxPlayers; i++)
        {
            if (_playerIds[i] > 0 && _playerIds[i] != _lastSnapshotIds[i])
            {
                _portraitDirty[i] = true;
                _lastSnapshotIds[i] = _playerIds[i];
            }
            else if (_playerIds[i] <= 0)
            {
                _lastSnapshotIds[i] = -1;
            }
        }
    }

    private void ApplyState(bool animate)
    {
        if (!_uiReady) return;
        RefreshLocalSlot();
        DetectPortraitChanges();
        bool lobby = _phase == PhaseLobby;
        bool pickPhase = _phase == PhasePick;
        bool cluePhase = _phase == PhaseClues;
        bool localPicker = _localSlot == _pickerSlot;
        if (_lobbyView != null) _lobbyView.SetActive(lobby);
        if (_gameView != null) _gameView.SetActive(!lobby);
        if (_leaveButton != null) _leaveButton.gameObject.SetActive(_localSlot >= 0);
        bool localHost = Networking.LocalPlayer != null && Networking.LocalPlayer.playerId == _hostId;
        if (_classicButton != null) _classicButton.gameObject.SetActive(localHost && PlayerCount() >= 2);
        if (_partyButton != null) _partyButton.gameObject.SetActive(localHost && PlayerCount() >= 2);
        if (_resetButton != null) _resetButton.gameObject.SetActive(localHost && !lobby);
        if (_modeText != null) _modeText.text = lobby ? PlayerCount() + " / 6 PLAYERS  ·  2 REQUIRED" : (_mode == ModeClassic ? "CLASSIC" : "PARTY");
        if (_roundText != null) _roundText.text = "ROUND " + _round + " / " + _maxRounds;
        if (_scoreText != null) _scoreText.text = "TEAM SCORE  " + _teamScore;
        if (_wordPickView != null) _wordPickView.SetActive(pickPhase && localPicker);
        if (_clueEntry != null) _clueEntry.SetActive(cluePhase && _localSlot >= 0 && !localPicker && _clueStates[_localSlot] == 0);
        if (_guessEntry != null) _guessEntry.SetActive(cluePhase && localPicker);
        if (_clueBoard != null) _clueBoard.SetActive(cluePhase || _phase == PhaseReveal);
        if (lobby)
        {
            if (_statusText != null) _statusText.text = _localSlot < 0 ? "SELECT AN OPEN SEAT TO JOIN" : (localHost ? "YOU ARE HOST · START WHEN READY" : "WAITING FOR HOST");
        }
        else if (pickPhase)
        {
            if (_statusText != null) _statusText.text = localPicker ? "PICK ONE OF THE 3 MYSTERY WORDS" : _playerNames[_pickerSlot] + " IS PICKING A WORD";
        }
        else if (cluePhase)
        {
            if (_statusText != null)
            {
                if (_localSlot < 0) _statusText.text = "PLAYERS ARE GIVING HINTS";
                else if (localPicker) _statusText.text = "READ THE HINTS · GUESS WHEN READY";
                else _statusText.text = _clueStates[_localSlot] == 0 ? "WRITE ONE UNIQUE HINT" : "HINT LOCKED";
            }
        }
        else if (_phase == PhaseReveal)
        {
            if (_statusText != null) _statusText.text = _lastResult > 0 ? "CORRECT · +1 POINT" : (_lastResult < 0 ? "INCORRECT · -1 POINT" : "SKIPPED · 0 POINTS");
        }
        else
        {
            if (_statusText != null) _statusText.text = "GAME COMPLETE · FINAL SCORE " + _teamScore;
        }
        if (pickPhase)
        {
            if (_wordText != null) _wordText.text = localPicker ? "PICK A WORD" : "PICKER CHOOSING";
        }
        else if (cluePhase)
        {
            if (_wordText != null) _wordText.text = localPicker ? "???" : _words[_wordIndex].ToUpper();
        }
        else if (_phase == PhaseReveal || _phase == PhaseGameOver)
        {
            if (_wordText != null) _wordText.text = _words[_wordIndex].ToUpper();
        }
        else
        {
            if (_wordText != null) _wordText.text = "MYSTERY WORD";
        }
        if (_feedbackText != null) _feedbackText.text = _phase == PhaseReveal ? _lastGuess.ToUpper() : "";
        for (int i = 0; i < MaxPlayers; i++)
        {
            bool occupied = _playerIds[i] > 0;
            if (_seatNames[i] != null) _seatNames[i].text = occupied ? _playerNames[i] : "JOIN";
            if (_seatPlaceholders[i] != null) _seatPlaceholders[i].SetActive(!occupied);
            if (_seatRoles[i] != null)
            {
                if (!occupied) _seatRoles[i].text = "OPEN SEAT";
                else if (_playerIds[i] == _hostId) _seatRoles[i].text = "HOST";
                else if (!lobby && i == _pickerSlot) _seatRoles[i].text = "PICKER";
                else _seatRoles[i].text = "PLAYER";
            }
            if (_seatFrames[i] != null)
            {
                Color accent = !occupied ? new Color(0.35f, 0.38f, 0.4f, 1f) : (i == _pickerSlot && !lobby ? new Color(0.18f, 0.75f, 0.95f, 1f) : new Color(0.55f, 0.9f, 0.45f, 1f));
                _seatFrames[i].color = new Color(accent.r * 0.14f, accent.g * 0.14f, accent.b * 0.14f, 0.98f);
            }
            if (_clueTexts[i] != null)
            {
                string clue = _clues[i];
                if (_clueStates[i] == 2) clue = "DUPLICATE";
                if (_clueStates[i] == 0) clue = "—";
                _clueTexts[i].text = clue.ToUpper();
                _clueTexts[i].color = _clueStates[i] == 2 ? new Color(1f, 0.3f, 0.3f, 1f) : Color.white;
            }
        }
        if (animate && _stage != null)
        {
            _entrance = 0f;
            _stage.localScale = Vector3.one * 0.96f;
        }
    }

    private void UpdateAnimation()
    {
        if (_entrance < 1f && _stage != null)
        {
            _entrance = Mathf.Min(1f, _entrance + Time.deltaTime * 4f);
            float eased = 1f - Mathf.Pow(1f - _entrance, 3f);
            _stage.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, eased);
        }
        _pulse += Time.deltaTime;
        float glow = 0.55f + Mathf.Sin(_pulse * 2.2f) * 0.12f;
        for (int i = 0; i < MaxPlayers; i++)
        {
            if (_playerIds[i] > 0 && i == _pickerSlot && _phase != PhaseLobby)
            {
                if (_seatFrames[i] != null)
                {
                    _seatFrames[i].color = new Color(0.04f, 0.22f, glow, 0.98f);
                }
            }
        }
    }

    private void UpdateTimer()
    {
        if (_timerText == null) return;
        if (_mode != ModeParty || _phaseDeadline <= 0d || (_phase != PhasePick && _phase != PhaseClues))
        {
            _timerText.text = _mode == ModeParty ? "PARTY TIMER" : "NO TIMER";
            return;
        }
        int remaining = Mathf.Max(0, Mathf.CeilToInt((float)(_phaseDeadline - Networking.GetServerTimeInSeconds())));
        _timerText.text = remaining + " SEC";
    }

    private void UpdatePortraits()
    {
        // Two-frame snapshot: disable camera from previous frame (RenderTexture retains image)
        if (_portraitRenderingSlot >= 0)
        {
            if (_portraitRenderingSlot < MaxPlayers && _portraitCameras[_portraitRenderingSlot] != null)
            {
                _portraitCameras[_portraitRenderingSlot].enabled = false;
            }
            _portraitRenderingSlot = -1;
            return;
        }
        // Find one dirty portrait to snapshot this frame
        for (int i = 0; i < MaxPlayers; i++)
        {
            if (!_portraitDirty[i]) continue;
            _portraitDirty[i] = false;
            if (_playerIds[i] <= 0 || _portraitCameras[i] == null) continue;
            VRCPlayerApi player = VRCPlayerApi.GetPlayerById(_playerIds[i]);
            if (player == null || !player.IsValid()) continue;
            // Head bone position (primary)
            Vector3 headPos = player.GetBonePosition(HumanBodyBones.Head);
            Quaternion headRot = player.GetBoneRotation(HumanBodyBones.Head);
            // Fallback 1: head tracking data (VR headset or desktop camera)
            if (headPos == Vector3.zero)
            {
                VRCPlayerApi.TrackingData tracking = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
                headPos = tracking.position;
                headRot = tracking.rotation;
            }
            // Fallback 2: headless avatar or unloaded avatar — use player position + eye height
            if (headPos == Vector3.zero)
            {
                headPos = player.GetPosition() + Vector3.up * 1.6f;
                headRot = player.GetRotation();
            }
            // The head bone sits at the base of the skull/neck — offset up to center the face
            Vector3 faceCenter = headPos + Vector3.up * 0.08f;
            // Place camera further back (0.9m) for a proper head-and-shoulders framing
            Vector3 camPos = faceCenter + headRot * Vector3.forward * 0.9f;
            // Raise camera slightly above face center and angle down to capture full head
            camPos += Vector3.up * 0.05f;
            _portraitCameras[i].transform.position = camPos;
            // Look at the face center from above
            Vector3 lookDir = (faceCenter - camPos).normalized;
            _portraitCameras[i].transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);
            _portraitCameras[i].enabled = true;
            _portraitRenderingSlot = i;
            return;
        }
    }

    public void ToggleTutorial()
    {
        _tutorialOpen = !_tutorialOpen;
        _tutorial.SetActive(_tutorialOpen);
    }

    public void CloseTutorial()
    {
        _tutorialOpen = false;
        _tutorial.SetActive(false);
    }

    public void JoinSeat0() { JoinSeat(0); }
    public void JoinSeat1() { JoinSeat(1); }
    public void JoinSeat2() { JoinSeat(2); }
    public void JoinSeat3() { JoinSeat(3); }
    public void JoinSeat4() { JoinSeat(4); }
    public void JoinSeat5() { JoinSeat(5); }

    private void JoinSeat(int slot)
    {
        if (_phase != PhaseLobby || _localSlot >= 0 || slot < 0 || slot >= MaxPlayers || _playerIds[slot] > 0) return;
        NetworkCalling.SendCustomNetworkEvent((IUdonEventReceiver)this, NetworkEventTarget.Owner, nameof(RequestJoin), slot);
    }

    [NetworkCallable]
    public void RequestJoin(int requestedSlot)
    {
        if (!Networking.IsOwner(gameObject) || _phase != PhaseLobby) return;
        VRCPlayerApi caller = NetworkCalling.CallingPlayer;
        if (caller == null || !caller.IsValid() || FindSlot(caller.playerId) >= 0) return;
        int slot = requestedSlot;
        if (slot < 0 || slot >= MaxPlayers || _playerIds[slot] > 0) slot = FirstOpenSlot();
        if (slot < 0) return;
        _playerIds[slot] = caller.playerId;
        _playerNames[slot] = caller.displayName;
        if (_hostId < 0) _hostId = caller.playerId;
        RequestSerialization();
        ApplyState(true);
    }

    public void LeaveGame()
    {
        if (_localSlot < 0) return;
        NetworkCalling.SendCustomNetworkEvent((IUdonEventReceiver)this, NetworkEventTarget.Owner, nameof(RequestLeave));
    }

    [NetworkCallable]
    public void RequestLeave()
    {
        if (!Networking.IsOwner(gameObject)) return;
        VRCPlayerApi caller = NetworkCalling.CallingPlayer;
        if (caller == null || !caller.IsValid()) return;
        int slot = FindSlot(caller.playerId);
        if (slot < 0) return;
        ClearSeat(slot);
        if (_hostId == caller.playerId) _hostId = FirstPlayerId();
        if (_phase != PhaseLobby && (slot == _pickerSlot || PlayerCount() < 2)) ResetState();
        RequestSerialization();
        ApplyState(true);
    }

    public void StartClassic()
    {
        NetworkCalling.SendCustomNetworkEvent((IUdonEventReceiver)this, NetworkEventTarget.Owner, nameof(RequestStart), ModeClassic);
    }

    public void StartParty()
    {
        NetworkCalling.SendCustomNetworkEvent((IUdonEventReceiver)this, NetworkEventTarget.Owner, nameof(RequestStart), ModeParty);
    }

    [NetworkCallable]
    public void RequestStart(int requestedMode)
    {
        if (!Networking.IsOwner(gameObject) || _phase != PhaseLobby || PlayerCount() < 2) return;
        VRCPlayerApi caller = NetworkCalling.CallingPlayer;
        if (!IsHost(caller)) return;
        _mode = requestedMode == ModeParty ? ModeParty : ModeClassic;
        _maxRounds = _mode == ModeParty ? 12 : 8;
        _round = 1;
        _teamScore = 0;
        _pickerSlot = NextOccupiedSlot(-1);
        BeginRound();
    }

    private void BeginRound()
    {
        ClearClues();
        _lastGuess = "";
        _lastResult = 0;
        GenerateWordChoices();
        _phase = PhasePick;
        _phaseDeadline = _mode == ModeParty ? Networking.GetServerTimeInSeconds() + 15d : 0d;
        RequestSerialization();
        ApplyState(true);
    }

    private void BeginClues()
    {
        _phase = PhaseClues;
        _phaseDeadline = _mode == ModeParty ? Networking.GetServerTimeInSeconds() + 45d : 0d;
        RequestSerialization();
        ApplyState(true);
    }

    private void GenerateWordChoices()
    {
        int w0 = Random.Range(0, _words.Length);
        int w1 = Random.Range(0, _words.Length);
        int w2 = Random.Range(0, _words.Length);
        if (w1 == w0) w1 = (w1 + 1) % _words.Length;
        if (w2 == w0 || w2 == w1) w2 = (w2 + 1) % _words.Length;
        if (w2 == w0 || w2 == w1) w2 = (w2 + 2) % _words.Length;
        _wordChoices[0] = w0;
        _wordChoices[1] = w1;
        _wordChoices[2] = w2;
    }

    public void PickWord0() { PickWord(0); }
    public void PickWord1() { PickWord(1); }
    public void PickWord2() { PickWord(2); }

    private void PickWord(int choiceIndex)
    {
        if (_phase != PhasePick || _localSlot != _pickerSlot) return;
        NetworkCalling.SendCustomNetworkEvent((IUdonEventReceiver)this, NetworkEventTarget.Owner, nameof(RequestPickWord), choiceIndex);
    }

    [NetworkCallable]
    public void RequestPickWord(int choiceIndex)
    {
        if (!Networking.IsOwner(gameObject) || _phase != PhasePick) return;
        VRCPlayerApi caller = NetworkCalling.CallingPlayer;
        if (caller == null || !caller.IsValid() || FindSlot(caller.playerId) != _pickerSlot) return;
        if (choiceIndex < 0 || choiceIndex > 2) return;
        _wordIndex = _wordChoices[choiceIndex];
        BeginClues();
    }

    public void SubmitClue()
    {
        if (_phase != PhaseClues || _localSlot < 0 || _localSlot == _pickerSlot || _clueInput == null) return;
        string clue = Normalize(_clueInput.text);
        if (!ValidClue(clue))
        {
            _statusText.text = "USE ONE REAL WORD · NOT THE MYSTERY WORD";
            return;
        }
        _clueInput.text = "";
        NetworkCalling.SendCustomNetworkEvent((IUdonEventReceiver)this, NetworkEventTarget.Owner, nameof(RequestClue), _round, clue);
    }

    [NetworkCallable]
    public void RequestClue(int eventRound, string clue)
    {
        if (!Networking.IsOwner(gameObject) || _phase != PhaseClues || eventRound != _round) return;
        VRCPlayerApi caller = NetworkCalling.CallingPlayer;
        if (caller == null || !caller.IsValid()) return;
        int slot = FindSlot(caller.playerId);
        clue = Normalize(clue);
        if (slot < 0 || slot == _pickerSlot || _clueStates[slot] != 0 || !ValidClue(clue)) return;
        _clues[slot] = clue;
        _clueStates[slot] = 1;
        CheckDuplicates();
        RequestSerialization();
        ApplyState(false);
    }

    private bool ValidClue(string clue)
    {
        if (clue == null || clue.Length < 2 || clue.Length > 24) return false;
        if (clue.Contains(" ")) return false;
        return clue != Normalize(_words[_wordIndex]);
    }

    private void CheckDuplicates()
    {
        for (int i = 0; i < MaxPlayers; i++)
        {
            if (_clueStates[i] == 2) _clueStates[i] = 1;
        }
        for (int i = 0; i < MaxPlayers; i++)
        {
            if (_clueStates[i] != 1) continue;
            for (int j = i + 1; j < MaxPlayers; j++)
            {
                if (_clueStates[j] == 1 && Normalize(_clues[i]) == Normalize(_clues[j]))
                {
                    _clueStates[i] = 2;
                    _clueStates[j] = 2;
                }
            }
        }
    }

    public void SubmitGuess()
    {
        if (_phase != PhaseClues || _localSlot != _pickerSlot || _guessInput == null) return;
        string guess = Normalize(_guessInput.text);
        if (guess.Length < 1) return;
        _guessInput.text = "";
        NetworkCalling.SendCustomNetworkEvent((IUdonEventReceiver)this, NetworkEventTarget.Owner, nameof(RequestGuess), _round, guess);
    }

    [NetworkCallable]
    public void RequestGuess(int eventRound, string guess)
    {
        if (!Networking.IsOwner(gameObject) || _phase != PhaseClues || eventRound != _round) return;
        VRCPlayerApi caller = NetworkCalling.CallingPlayer;
        if (caller == null || !caller.IsValid() || FindSlot(caller.playerId) != _pickerSlot) return;
        guess = Normalize(guess);
        ResolveRound(guess == Normalize(_words[_wordIndex]) ? 1 : -1, guess);
    }

    public void SkipGuess()
    {
        if (_phase != PhaseClues || _localSlot != _pickerSlot) return;
        NetworkCalling.SendCustomNetworkEvent((IUdonEventReceiver)this, NetworkEventTarget.Owner, nameof(RequestSkip), _round);
    }

    [NetworkCallable]
    public void RequestSkip(int eventRound)
    {
        if (!Networking.IsOwner(gameObject) || _phase != PhaseClues || eventRound != _round) return;
        VRCPlayerApi caller = NetworkCalling.CallingPlayer;
        if (caller == null || !caller.IsValid() || FindSlot(caller.playerId) != _pickerSlot) return;
        ResolveRound(0, "SKIPPED");
    }

    private void ResolveRound(int result, string guess)
    {
        _lastResult = result;
        _lastGuess = guess;
        if (result > 0) _teamScore++;
        else if (result < 0) _teamScore--;
        _phase = PhaseReveal;
        _phaseDeadline = 0d;
        RequestSerialization();
        ApplyState(true);
        SendCustomEventDelayedSeconds(nameof(AdvanceRound), 4f);
    }

    public void AdvanceRound()
    {
        if (!Networking.IsOwner(gameObject) || _phase != PhaseReveal) return;
        if (_round >= _maxRounds)
        {
            _phase = PhaseGameOver;
            RequestSerialization();
            ApplyState(true);
            return;
        }
        _round++;
        _pickerSlot = NextOccupiedSlot(_pickerSlot);
        BeginRound();
    }

    public void ResetGame()
    {
        if (Networking.LocalPlayer == null || Networking.LocalPlayer.playerId != _hostId) return;
        NetworkCalling.SendCustomNetworkEvent((IUdonEventReceiver)this, NetworkEventTarget.Owner, nameof(RequestReset));
    }

    [NetworkCallable]
    public void RequestReset()
    {
        if (!Networking.IsOwner(gameObject)) return;
        VRCPlayerApi caller = NetworkCalling.CallingPlayer;
        if (!IsHost(caller)) return;
        ResetState();
        RequestSerialization();
        ApplyState(true);
    }

    public void AdminReset()
    {
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
        ResetState();
        RequestSerialization();
        ApplyState(true);
    }

    private void ResetState()
    {
        _phase = PhaseLobby;
        _mode = ModeClassic;
        _round = 0;
        _maxRounds = 8;
        _pickerSlot = -1;
        _teamScore = 0;
        _lastGuess = "";
        _lastResult = 0;
        _phaseDeadline = 0d;
        ClearClues();
    }

    private void ClearClues()
    {
        for (int i = 0; i < MaxPlayers; i++)
        {
            _clues[i] = "";
            _clueStates[i] = 0;
        }
    }

    private string Normalize(string value)
    {
        if (value == null) return "";
        return value.Trim().ToLower();
    }

    private void LoadWords()
    {
        _words = new string[]
        {
            "airport","anchor","apple","artist","astronaut","bakery","balloon","battery","beach","bicycle",
            "blanket","bridge","butterfly","camera","candle","castle","cereal","cheese","cinema","clock",
            "cloud","coffee","comet","compass","concert","cookie","crown","crystal","desert","diamond",
            "dinosaur","doctor","dragon","elevator","engine","feather","festival","firework","forest","fountain",
            "galaxy","garden","ghost","guitar","hammer","harbor","helmet","honey","hospital","island",
            "jacket","jungle","keyboard","kingdom","lantern","library","lightning","magnet","market","mermaid",
            "mirror","monster","moonlight","mountain","museum","notebook","ocean","painter","panda","parade",
            "penguin","piano","picnic","pirate","planet","popcorn","portal","puzzle","rainbow","restaurant",
            "robot","rocket","sandwich","satellite","school","shadow","shipwreck","snowman","spaceship","stadium",
            "storm","submarine","sunflower","telescope","theater","thunder","tiger","train","treasure","tunnel",
            "umbrella","unicorn","volcano","waterfall","whistle","wizard","zebra","backpack","campfire","carnival",
            "chocolate","detective","dolphin","fireplace","headphones","helicopter","lighthouse","microphone","octopus","parachute",
            "photograph","playground","scarecrow","skateboard","snowflake","suitcase","superhero","telephone","tornado","windmill"
        };
    }
}
