using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Persistence;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class CodeBreakerGame : UdonSharpBehaviour
{
    private const int PhaseIdle = 0;
    private const int PhaseLobby = 1;
    private const int PhaseSolo = 2;
    private const int PhasePvpSetup = 3;
    private const int PhasePvpTurn = 4;
    private const int PhaseResult = 5;
    private const int SoloLength = 10;
    private const int PvpLength = 4;
    private const int SoloAttempts = 7;
    private const string PersonalBestKey = "Chromix.CrackTheCode.SoloBestSeconds.v1";

    [UdonSynced] private int phase;
    [UdonSynced] private int roundId;
    [UdonSynced] private int player1Id;
    [UdonSynced] private int player2Id;
    [UdonSynced] private string player1Name = "";
    [UdonSynced] private string player2Name = "";
    [UdonSynced] private double lobbyEndServerTime;
    [UdonSynced] private bool player1Ready;
    [UdonSynced] private bool player2Ready;
    [UdonSynced] private int activeSlot;
    [UdonSynced] private int winnerSlot;
    [UdonSynced] private int player1LockedMask;
    [UdonSynced] private int player2LockedMask;
    [UdonSynced] private int player1LockedDigits;
    [UdonSynced] private int player2LockedDigits;
    [UdonSynced] private int lastGuessPacked;
    [UdonSynced] private int lastGuessMask;
    [UdonSynced] private int lastGuesserSlot;
    [UdonSynced] private int guessSequence;

    private Text modeText;
    private Text playersText;
    private Text statusText;
    private Text timerText;
    private Text attemptsText;
    private Text personalBestText;
    private Text joinLabel;
    private Text resetLabel;
    private GameObject joinButton;
    private GameObject resetButton;
    private GameObject keypadPanel;
    private GameObject digitGrid;
    private readonly Text[] digitTexts = new Text[SoloLength];
    private readonly Image[] digitImages = new Image[SoloLength];
    private readonly int[] secret = new int[SoloLength];
    private readonly int[] currentGuess = new int[SoloLength];
    private readonly int[] localLockedDigitValues = new int[SoloLength];

    private Color cellIdle;
    private Color cellActive;
    private Color cellGreen;
    private Color cellRed;
    private int localSlot;
    private int localSecretLength;
    private int currentFilledMask;
    private int localLockedMask;
    private int localLockedDigits;
    private int redFlashMask;
    private int attemptsUsed;
    private int observedRoundId = -1;
    private int observedPhase = -1;
    private int observedGuessSequence = -1;
    private bool localSecretReady;
    private bool waitingForResult;
    private bool soloRunning;
    private bool playerDataReady;
    private double soloStartServerTime;
    private float personalBest;
    private float pendingBest;
    private string localResult = "";

    private void Start()
    {
        Debug.Log("[CrackTheCode] Start() called");
        cellIdle = new Color(0.055f, 0.11f, 0.17f, 1f);
        cellActive = new Color(0.03f, 0.42f, 0.55f, 1f);
        cellGreen = new Color(0.08f, 0.68f, 0.38f, 1f);
        cellRed = new Color(0.82f, 0.12f, 0.19f, 1f);
        FindUi();
        Debug.Log("[CrackTheCode] FindUi done. modeText=" + (modeText != null) + " statusText=" + (statusText != null) + " joinButton=" + (joinButton != null));
        ApplyState(true);
    }

    private void Update()
    {
        if (Networking.IsOwner(gameObject) && phase == PhaseLobby && Networking.GetServerTimeInSeconds() >= lobbyEndServerTime)
        {
            if (player1Id > 0 && player2Id == 0)
            {
                phase = PhaseSolo;
                roundId++;
                RequestSerialization();
                ApplyState(true);
            }
            else if (player1Id == 0)
            {
                ResetSyncedState();
            }
        }

        if (phase == PhaseLobby)
        {
            double remaining = lobbyEndServerTime - Networking.GetServerTimeInSeconds();
            if (remaining < 0d) remaining = 0d;
            if (timerText != null) timerText.text = "MATCH WINDOW  " + remaining.ToString("0.0") + "s";
        }
        else if (phase == PhaseSolo && soloRunning)
        {
            double elapsed = Networking.GetServerTimeInSeconds() - soloStartServerTime;
            if (elapsed < 0d) elapsed = 0d;
            if (timerText != null) timerText.text = "TIME  " + FormatTime((float)elapsed);
        }
    }

    public override void OnDeserialization() { ApplyState(false); }
    public override void OnOwnershipTransferred(VRCPlayerApi player) { ApplyState(false); }

    public override void OnPlayerRestored(VRCPlayerApi player)
    {
        if (!Utilities.IsValid(player) || !player.isLocal) return;
        personalBest = PlayerData.GetFloat(player, PersonalBestKey);
        playerDataReady = true;
        if (pendingBest > 0f && (personalBest <= 0f || pendingBest < personalBest)) SavePersonalBest(pendingBest);
        RefreshPersonalBest();
    }

    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        if (!Networking.IsOwner(gameObject) || !Utilities.IsValid(player)) return;
        int leavingSlot = player.playerId == player1Id ? 1 : player.playerId == player2Id ? 2 : 0;
        if (leavingSlot == 0) return;
        if (phase == PhasePvpSetup || phase == PhasePvpTurn)
        {
            winnerSlot = leavingSlot == 1 ? 2 : 1;
            phase = PhaseResult;
            RequestSerialization();
            ApplyState(true);
            return;
        }
        ResetSyncedState();
    }

    public void JoinGame()
    {
        Debug.Log("[CrackTheCode] JoinGame clicked. localSlot=" + localSlot + " phase=" + phase);
        if (localSlot != 0 || phase == PhaseSolo || phase == PhasePvpSetup || phase == PhasePvpTurn) return;
        Debug.Log("[CrackTheCode] Sending RequestJoin network event. IsOwner=" + Networking.IsOwner(gameObject));
        NetworkCalling.SendCustomNetworkEvent((IUdonEventReceiver)this, NetworkEventTarget.Owner, nameof(RequestJoin));
    }

    [NetworkCallable]
    public void RequestJoin()
    {
        Debug.Log("[CrackTheCode] RequestJoin received. IsOwner=" + Networking.IsOwner(gameObject) + " phase=" + phase);
        if (!Networking.IsOwner(gameObject)) return;
        VRCPlayerApi caller = NetworkCalling.CallingPlayer;
        Debug.Log("[CrackTheCode] caller valid=" + Utilities.IsValid(caller) + " callerId=" + (Utilities.IsValid(caller) ? caller.playerId : -1));
        if (!Utilities.IsValid(caller) || caller.playerId == player1Id || caller.playerId == player2Id) return;
        if (phase == PhaseIdle || (phase == PhaseResult && player1Id == 0))
        {
            player1Id = caller.playerId;
            player1Name = caller.displayName;
            player2Id = 0;
            player2Name = "";
            player1Ready = false;
            player2Ready = false;
            player1LockedMask = 0;
            player2LockedMask = 0;
            player1LockedDigits = 0;
            player2LockedDigits = 0;
            winnerSlot = 0;
            activeSlot = 0;
            phase = PhaseLobby;
            lobbyEndServerTime = Networking.GetServerTimeInSeconds() + 8d;
            roundId++;
        }
        else if (phase == PhaseLobby && player2Id == 0)
        {
            player2Id = caller.playerId;
            player2Name = caller.displayName;
            phase = PhasePvpSetup;
            roundId++;
        }
        else return;
        RequestSerialization();
        ApplyState(true);
    }

    public void Digit0() { AddDigit(0); }
    public void Digit1() { AddDigit(1); }
    public void Digit2() { AddDigit(2); }
    public void Digit3() { AddDigit(3); }
    public void Digit4() { AddDigit(4); }
    public void Digit5() { AddDigit(5); }
    public void Digit6() { AddDigit(6); }
    public void Digit7() { AddDigit(7); }
    public void Digit8() { AddDigit(8); }
    public void Digit9() { AddDigit(9); }

    public void EraseDigit()
    {
        if (!CanEnterDigits() || waitingForResult) return;
        for (int i = localSecretLength - 1; i >= 0; i--)
        {
            int bit = 1 << i;
            if ((currentFilledMask & bit) != 0 && (localLockedMask & bit) == 0)
            {
                currentFilledMask &= ~bit;
                currentGuess[i] = 0;
                break;
            }
        }
        RefreshDigits();
    }

    public void ConfirmEntry()
    {
        if (!CanEnterDigits() || waitingForResult) return;
        int fullMask = (1 << localSecretLength) - 1;
        if ((currentFilledMask | localLockedMask) != fullMask)
        {
            SetStatus("ENTER EVERY UNLOCKED DIGIT");
            return;
        }
        if (phase == PhasePvpSetup)
        {
            for (int i = 0; i < PvpLength; i++) secret[i] = currentGuess[i];
            localSecretReady = true;
            currentFilledMask = 0;
            ClearGuessArray();
            RefreshDigits();
            SetStatus("CODE SEALED · WAITING FOR OPPONENT");
            NetworkCalling.SendCustomNetworkEvent((IUdonEventReceiver)this, NetworkEventTarget.Owner, nameof(RequestReady), roundId, localSlot);
            return;
        }
        if (phase == PhaseSolo)
        {
            SubmitSoloGuess();
            return;
        }
        if (phase == PhasePvpTurn)
        {
            int packed = PackDigits(currentGuess, PvpLength);
            waitingForResult = true;
            SetStatus("TRANSMITTING GUESS...");
            NetworkCalling.SendCustomNetworkEvent((IUdonEventReceiver)this, NetworkEventTarget.All, nameof(ReceivePvpGuess), roundId, localSlot, packed);
            SendCustomEventDelayedSeconds(nameof(CancelPendingGuess), 3f);
        }
    }

    [NetworkCallable]
    public void RequestReady(int eventRoundId, int slot)
    {
        if (!Networking.IsOwner(gameObject) || phase != PhasePvpSetup || eventRoundId != roundId) return;
        VRCPlayerApi caller = NetworkCalling.CallingPlayer;
        if (!IsPlayerForSlot(caller, slot)) return;
        if (slot == 1) player1Ready = true;
        else if (slot == 2) player2Ready = true;
        else return;
        if (player1Ready && player2Ready)
        {
            activeSlot = Random.Range(0, 2) == 0 ? 1 : 2;
            phase = PhasePvpTurn;
        }
        RequestSerialization();
        ApplyState(true);
    }

    [NetworkCallable]
    public void ReceivePvpGuess(int eventRoundId, int guesserSlot, int packedGuess)
    {
        if (phase != PhasePvpTurn || eventRoundId != roundId || guesserSlot != activeSlot) return;
        VRCPlayerApi caller = NetworkCalling.CallingPlayer;
        if (!IsPlayerForSlot(caller, guesserSlot)) return;
        int defenderSlot = guesserSlot == 1 ? 2 : 1;
        if (localSlot != defenderSlot || !localSecretReady) return;
        int exactMask = 0;
        for (int i = 0; i < PvpLength; i++)
        {
            if (DigitAt(packedGuess, i) == secret[i]) exactMask |= 1 << i;
        }
        NetworkCalling.SendCustomNetworkEvent((IUdonEventReceiver)this, NetworkEventTarget.Owner, nameof(ReportGuessResult), eventRoundId, guesserSlot, packedGuess, exactMask);
    }

    [NetworkCallable]
    public void ReportGuessResult(int eventRoundId, int guesserSlot, int packedGuess, int exactMask)
    {
        if (!Networking.IsOwner(gameObject) || phase != PhasePvpTurn || eventRoundId != roundId || guesserSlot != activeSlot) return;
        int defenderSlot = guesserSlot == 1 ? 2 : 1;
        VRCPlayerApi caller = NetworkCalling.CallingPlayer;
        if (!IsPlayerForSlot(caller, defenderSlot)) return;
        if (guesserSlot == 1)
        {
            player1LockedMask |= exactMask;
            player1LockedDigits = MergePackedDigits(player1LockedDigits, packedGuess, exactMask, PvpLength);
            if (player1LockedMask == 15) winnerSlot = 1;
        }
        else
        {
            player2LockedMask |= exactMask;
            player2LockedDigits = MergePackedDigits(player2LockedDigits, packedGuess, exactMask, PvpLength);
            if (player2LockedMask == 15) winnerSlot = 2;
        }
        lastGuessPacked = packedGuess;
        lastGuessMask = exactMask;
        lastGuesserSlot = guesserSlot;
        guessSequence++;
        if (winnerSlot != 0) phase = PhaseResult;
        else activeSlot = defenderSlot;
        RequestSerialization();
        ApplyState(true);
    }

    public void ResetGame()
    {
        if (localSlot == 0) return;
        NetworkCalling.SendCustomNetworkEvent((IUdonEventReceiver)this, NetworkEventTarget.Owner, nameof(RequestReset), roundId);
    }

    // Admin/Moderator forced reset - bypasses the localSlot participant check.
    // Called by the ChromixModTool; caller is responsible for admin/mod validation.
    public void AdminReset()
    {
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
        ResetSyncedState();
    }

    [NetworkCallable]
    public void RequestReset(int eventRoundId)
    {
        if (!Networking.IsOwner(gameObject) || eventRoundId != roundId) return;
        VRCPlayerApi caller = NetworkCalling.CallingPlayer;
        if (!IsPlayerForSlot(caller, 1) && !IsPlayerForSlot(caller, 2)) return;
        ResetSyncedState();
    }

    private void SubmitSoloGuess()
    {
        attemptsUsed++;
        int exactMask = 0;
        for (int i = 0; i < SoloLength; i++)
        {
            if ((localLockedMask & (1 << i)) != 0 || currentGuess[i] == secret[i]) exactMask |= 1 << i;
        }
        localLockedMask = exactMask;
        for (int i = 0; i < SoloLength; i++)
        {
            if ((exactMask & (1 << i)) != 0) localLockedDigitValues[i] = currentGuess[i];
        }
        redFlashMask = ((1 << SoloLength) - 1) & ~exactMask;
        RefreshDigits();
        if (exactMask == (1 << SoloLength) - 1)
        {
            soloRunning = false;
            float elapsed = (float)(Networking.GetServerTimeInSeconds() - soloStartServerTime);
            if (elapsed < 0f) elapsed = 0f;
            localResult = "ACCESS GRANTED · " + FormatTime(elapsed);
            SetStatus(localResult);
            RecordPersonalBest(elapsed);
            if (resetButton != null) resetButton.SetActive(true);
            NetworkCalling.SendCustomNetworkEvent((IUdonEventReceiver)this, NetworkEventTarget.Owner, nameof(ReportSoloFinished), roundId, true);
            return;
        }
        if (attemptsUsed >= SoloAttempts)
        {
            soloRunning = false;
            localResult = "SYSTEM LOCKED · CODE " + SecretAsText(SoloLength);
            SetStatus(localResult);
            if (resetButton != null) resetButton.SetActive(true);
            NetworkCalling.SendCustomNetworkEvent((IUdonEventReceiver)this, NetworkEventTarget.Owner, nameof(ReportSoloFinished), roundId, false);
            return;
        }
        SetStatus("RED SIGNALS CLEARING...");
        SendCustomEventDelayedSeconds(nameof(ClearRedFlash), 0.75f);
        RefreshAttempts();
    }

    [NetworkCallable]
    public void ReportSoloFinished(int eventRoundId, bool solved)
    {
        if (!Networking.IsOwner(gameObject) || phase != PhaseSolo || eventRoundId != roundId) return;
        VRCPlayerApi caller = NetworkCalling.CallingPlayer;
        if (!IsPlayerForSlot(caller, 1)) return;
        winnerSlot = solved ? 1 : 0;
        phase = PhaseResult;
        RequestSerialization();
        ApplyState(true);
    }

    public void CancelPendingGuess()
    {
        if (!waitingForResult || phase != PhasePvpTurn || activeSlot != localSlot) return;
        waitingForResult = false;
        SetStatus("NETWORK DELAY · CONFIRM TO RETRY");
    }

    public void ClearRedFlash()
    {
        for (int i = 0; i < localSecretLength; i++)
        {
            int bit = 1 << i;
            if ((redFlashMask & bit) != 0)
            {
                currentFilledMask &= ~bit;
                currentGuess[i] = 0;
            }
        }
        redFlashMask = 0;
        waitingForResult = false;
        RefreshDigits();
        if (phase == PhaseSolo) SetStatus("DECRYPT THE REMAINING POSITIONS");
        else ApplyState(false);
    }

    private void AddDigit(int digit)
    {
        if (!CanEnterDigits() || waitingForResult || digit < 0 || digit > 9) return;
        for (int i = 0; i < localSecretLength; i++)
        {
            int bit = 1 << i;
            if ((localLockedMask & bit) == 0 && (currentFilledMask & bit) == 0)
            {
                currentGuess[i] = digit;
                currentFilledMask |= bit;
                break;
            }
        }
        RefreshDigits();
    }

    private bool CanEnterDigits()
    {
        if (localSlot == 0) return false;
        if (phase == PhaseSolo) return soloRunning;
        if (phase == PhasePvpSetup) return !localSecretReady;
        return phase == PhasePvpTurn && activeSlot == localSlot;
    }

    private void ApplyState(bool force)
    {
        VRCPlayerApi localPlayer = Networking.LocalPlayer;
        localSlot = Utilities.IsValid(localPlayer) && localPlayer.playerId == player1Id ? 1 : Utilities.IsValid(localPlayer) && localPlayer.playerId == player2Id ? 2 : 0;
        bool roundChanged = observedRoundId != roundId;
        bool phaseChanged = observedPhase != phase;
        if (roundChanged)
        {
            observedRoundId = roundId;
            localSecretReady = false;
            waitingForResult = false;
            currentFilledMask = 0;
            localLockedMask = 0;
            localLockedDigits = 0;
            redFlashMask = 0;
            attemptsUsed = 0;
            localResult = "";
            ClearGuessArray();
            ClearLocalLockedDigits();
        }
        if (phase == PhaseSolo && localSlot == 1 && (roundChanged || phaseChanged)) BeginSoloLocal();
        if (phase == PhasePvpSetup && localSlot != 0 && (roundChanged || phaseChanged)) BeginPvpSetupLocal();
        if (phase == PhasePvpTurn && localSlot != 0)
        {
            localSecretLength = PvpLength;
            localLockedMask = localSlot == 1 ? player1LockedMask : player2LockedMask;
            localLockedDigits = localSlot == 1 ? player1LockedDigits : player2LockedDigits;
            if (observedGuessSequence != guessSequence && lastGuesserSlot == localSlot)
            {
                observedGuessSequence = guessSequence;
                UnpackDigits(lastGuessPacked, currentGuess, PvpLength);
                currentFilledMask = 15;
                redFlashMask = 15 & ~lastGuessMask;
                waitingForResult = false;
                RefreshDigits();
                if (redFlashMask != 0 && phase != PhaseResult) SendCustomEventDelayedSeconds(nameof(ClearRedFlash), 0.75f);
            }
        }
        observedPhase = phase;
        RefreshSharedLabels();
        RefreshControls();
        RefreshDigits();
        RefreshAttempts();
        RefreshPersonalBest();
    }

    private void BeginSoloLocal()
    {
        localSecretLength = SoloLength;
        for (int i = 0; i < SoloLength; i++) secret[i] = Random.Range(0, 10);
        localSecretReady = true;
        soloRunning = true;
        soloStartServerTime = Networking.GetServerTimeInSeconds();
        attemptsUsed = 0;
        currentFilledMask = 0;
        localLockedMask = 0;
        localLockedDigits = 0;
        ClearGuessArray();
        ClearLocalLockedDigits();
    }

    private void BeginPvpSetupLocal()
    {
        localSecretLength = PvpLength;
        localSecretReady = false;
        currentFilledMask = 0;
        localLockedMask = 0;
        localLockedDigits = 0;
        ClearGuessArray();
    }

    private void RefreshSharedLabels()
    {
        if (modeText != null)
        {
            if (phase == PhaseSolo) modeText.text = "SOLO · 10 DIGITS";
            else if (phase == PhasePvpSetup || phase == PhasePvpTurn) modeText.text = "PVP · 4 DIGITS";
            else modeText.text = "LOCAL-FIRST TERMINAL";
        }
        if (playersText != null)
        {
            string p1 = player1Id == 0 ? "OPEN" : player1Name;
            string p2 = player2Id == 0 ? "OPEN" : player2Name;
            playersText.text = "P1  " + p1 + "     //     P2  " + p2;
        }
        if (phase == PhaseIdle) SetStatus("LOGIC PROTOCOL READY");
        else if (phase == PhaseLobby) SetStatus(localSlot == 1 ? "WAITING FOR A CHALLENGER" : "JOIN BEFORE THE WINDOW CLOSES");
        else if (phase == PhaseSolo && localSlot != 1) SetStatus("SOLO RUN IN PROGRESS");
        else if (phase == PhaseSolo && soloRunning) SetStatus("DECRYPT THE 10-DIGIT CODE");
        else if (phase == PhasePvpSetup && localSlot == 0) SetStatus("PLAYERS ARE SEALING PRIVATE CODES");
        else if (phase == PhasePvpSetup && localSecretReady) SetStatus("CODE SEALED · WAITING FOR OPPONENT");
        else if (phase == PhasePvpSetup) SetStatus("ENTER YOUR PRIVATE 4-DIGIT CODE");
        else if (phase == PhasePvpTurn && localSlot == activeSlot) SetStatus("YOUR TURN · CRACK THE CODE");
        else if (phase == PhasePvpTurn && localSlot != 0) SetStatus("OPPONENT IS ANALYZING");
        else if (phase == PhasePvpTurn) SetStatus("PVP DECRYPTION IN PROGRESS");
        else if (phase == PhaseResult && localResult != "") SetStatus(localResult);
        else if (phase == PhaseResult && winnerSlot != 0) SetStatus(winnerSlot == localSlot ? "ACCESS GRANTED · YOU WIN" : "CODE BREACHED · " + WinnerName() + " WINS");
        else if (phase == PhaseResult) SetStatus("SYSTEM LOCKED");
        if (timerText != null && phase != PhaseLobby && !(phase == PhaseSolo && soloRunning)) timerText.text = phase == PhaseResult ? "ROUND COMPLETE" : "SECURE CHANNEL";
    }

    private void RefreshControls()
    {
        bool canJoin = localSlot == 0 && (phase == PhaseIdle || (phase == PhaseLobby && player2Id == 0));
        if (joinButton != null) joinButton.SetActive(canJoin);
        if (joinLabel != null) joinLabel.text = phase == PhaseLobby ? "JOIN AS CHALLENGER" : "JOIN GAME";
        if (keypadPanel != null) keypadPanel.SetActive(CanEnterDigits());
        if (resetButton != null) resetButton.SetActive(phase == PhaseResult && localSlot != 0);
        if (resetLabel != null) resetLabel.text = "NEW ROUND";
        if (digitGrid != null) digitGrid.SetActive(localSlot != 0 && phase != PhaseIdle && phase != PhaseLobby);
    }

    private void RefreshDigits()
    {
        int length = localSecretLength > 0 ? localSecretLength : PvpLength;
        for (int i = 0; i < SoloLength; i++)
        {
            if (digitTexts[i] == null || digitImages[i] == null) continue;
            digitImages[i].gameObject.SetActive(i < length);
            if (i >= length) continue;
            int bit = 1 << i;
            bool locked = (localLockedMask & bit) != 0;
            bool filled = (currentFilledMask & bit) != 0;
            bool red = (redFlashMask & bit) != 0;
            if (locked)
            {
                digitTexts[i].text = (phase == PhaseSolo ? localLockedDigitValues[i] : DigitAt(localLockedDigits, i)).ToString();
                digitImages[i].color = cellGreen;
            }
            else if (filled)
            {
                digitTexts[i].text = currentGuess[i].ToString();
                digitImages[i].color = red ? cellRed : cellActive;
            }
            else
            {
                digitTexts[i].text = "·";
                digitImages[i].color = cellIdle;
            }
        }
    }

    private void RefreshAttempts()
    {
        if (attemptsText == null) return;
        if (phase == PhaseSolo) attemptsText.text = "ATTEMPT  " + Mathf.Min(attemptsUsed + 1, SoloAttempts) + " / " + SoloAttempts;
        else if (phase == PhasePvpTurn) attemptsText.text = "TURN  " + (activeSlot == 1 ? player1Name : player2Name);
        else if (phase == PhasePvpSetup) attemptsText.text = (player1Ready ? "P1 READY" : "P1 SETTING") + "     " + (player2Ready ? "P2 READY" : "P2 SETTING");
        else attemptsText.text = "EXACT POSITION MATCHES ONLY";
    }

    private void FindUi()
    {
        Transform asset = transform.parent;
        Transform canvas = asset != null ? asset.Find("UI_Canvas") : null;
        if (canvas == null) return;
        modeText = FindText(canvas, "Frame/Header/ModeText");
        playersText = FindText(canvas, "Frame/Header/PlayersText");
        statusText = FindText(canvas, "Frame/StatusCard/StatusText");
        timerText = FindText(canvas, "Frame/StatusCard/TimerText");
        attemptsText = FindText(canvas, "Frame/StatusCard/AttemptsText");
        personalBestText = FindText(canvas, "Frame/Footer/PersonalBestText");
        joinLabel = FindText(canvas, "Frame/JoinButton/Label");
        resetLabel = FindText(canvas, "Frame/Footer/ResetButton/Label");
        Transform join = canvas.Find("Frame/JoinButton");
        Transform reset = canvas.Find("Frame/Footer/ResetButton");
        Transform keypad = canvas.Find("Frame/KeypadPanel");
        Transform digits = canvas.Find("Frame/DigitGrid");
        joinButton = join != null ? join.gameObject : null;
        resetButton = reset != null ? reset.gameObject : null;
        keypadPanel = keypad != null ? keypad.gameObject : null;
        digitGrid = digits != null ? digits.gameObject : null;
        for (int i = 0; i < SoloLength; i++)
        {
            Transform cell = digits != null ? digits.Find("Digit_" + i) : null;
            if (cell == null) continue;
            digitImages[i] = cell.GetComponent<Image>();
            Transform label = cell.Find("Label");
            if (label != null) digitTexts[i] = label.GetComponent<Text>();
        }
    }

    private Text FindText(Transform root, string path)
    {
        Transform target = root.Find(path);
        return target != null ? target.GetComponent<Text>() : null;
    }

    private void ResetSyncedState()
    {
        phase = PhaseIdle;
        roundId++;
        player1Id = 0;
        player2Id = 0;
        player1Name = "";
        player2Name = "";
        lobbyEndServerTime = 0d;
        player1Ready = false;
        player2Ready = false;
        activeSlot = 0;
        winnerSlot = 0;
        player1LockedMask = 0;
        player2LockedMask = 0;
        player1LockedDigits = 0;
        player2LockedDigits = 0;
        lastGuessPacked = 0;
        lastGuessMask = 0;
        lastGuesserSlot = 0;
        guessSequence = 0;
        RequestSerialization();
        ApplyState(true);
    }

    private bool IsPlayerForSlot(VRCPlayerApi player, int slot)
    {
        if (!Utilities.IsValid(player)) return false;
        return slot == 1 ? player.playerId == player1Id : slot == 2 && player.playerId == player2Id;
    }

    private int PackDigits(int[] digits, int length)
    {
        int packed = 0;
        int place = 1;
        for (int i = 0; i < length; i++)
        {
            packed += digits[i] * place;
            place *= 10;
        }
        return packed;
    }

    private void UnpackDigits(int packed, int[] target, int length)
    {
        for (int i = 0; i < length; i++)
        {
            target[i] = packed % 10;
            packed /= 10;
        }
    }

    private int DigitAt(int packed, int index)
    {
        for (int i = 0; i < index; i++) packed /= 10;
        return packed % 10;
    }

    private int MergePackedDigits(int existing, int incoming, int mask, int length)
    {
        int[] merged = new int[SoloLength];
        UnpackDigits(existing, merged, length);
        for (int i = 0; i < length; i++)
        {
            if ((mask & (1 << i)) != 0) merged[i] = DigitAt(incoming, i);
        }
        return PackDigits(merged, length);
    }

    private void ClearGuessArray()
    {
        for (int i = 0; i < SoloLength; i++) currentGuess[i] = 0;
    }

    private void ClearLocalLockedDigits()
    {
        for (int i = 0; i < SoloLength; i++) localLockedDigitValues[i] = 0;
    }

    private string SecretAsText(int length)
    {
        string value = "";
        for (int i = 0; i < length; i++) value += secret[i].ToString();
        return value;
    }

    private string WinnerName() { return winnerSlot == 1 ? player1Name : winnerSlot == 2 ? player2Name : ""; }
    private void SetStatus(string value) { if (statusText != null) statusText.text = value; }

    private string FormatTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        float remainder = seconds - minutes * 60f;
        return minutes.ToString("00") + ":" + remainder.ToString("00.00");
    }

    private void RecordPersonalBest(float elapsed)
    {
        if (!playerDataReady)
        {
            if (pendingBest <= 0f || elapsed < pendingBest) pendingBest = elapsed;
            return;
        }
        if (personalBest > 0f && elapsed >= personalBest) return;
        SavePersonalBest(elapsed);
    }

    private void SavePersonalBest(float elapsed)
    {
        personalBest = elapsed;
        pendingBest = 0f;
        PlayerData.SetFloat(PersonalBestKey, personalBest);
        RefreshPersonalBest();
    }

    private void RefreshPersonalBest()
    {
        if (personalBestText == null) return;
        if (!playerDataReady) personalBestText.text = "PERSONAL BEST  LOADING...";
        else if (personalBest <= 0f) personalBestText.text = "PERSONAL BEST  --:--.--";
        else personalBestText.text = "PERSONAL BEST  " + FormatTime(personalBest);
    }
}
