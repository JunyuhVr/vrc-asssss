using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using VRC.SDK3.Components;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class ChromixModTool : UdonSharpBehaviour
{
    [Header("Jail Zone")]
    public Transform _jailZone;

    [Header("Hardcoded Admins (display names)")]
    private readonly string[] _hardcodedAdmins = {
        "Casey_tt",
        "Designaやanͥdͣaͫ",
        "TheMinors4Realz",
        "Junyuh"
    };

    [Header("Colors")]
    private Color _cAdmin = new Color(0.157f, 0.847f, 0.961f, 1f);    // cyan
    private Color _cModerator = new Color(0.961f, 0.157f, 0.847f, 1f); // magenta
    private Color _cPlayer = new Color(0.6f, 0.7f, 0.8f, 1f);          // grey-blue
    private Color _cBanned = new Color(0.961f, 0.220f, 0.220f, 1f);    // red
    private Color _cSelected = new Color(1f, 0.8f, 0.2f, 1f);          // gold
    private Color _cNormal = new Color(0.078f, 0.157f, 0.220f, 1f);    // dark
    private Color _cDim = new Color(0.039f, 0.082f, 0.125f, 1f);       // darker
    private Color _cBG = new Color(0.055f, 0.114f, 0.169f, 1f);        // background
    private Color _cAccent = new Color(0.157f, 0.847f, 0.961f, 1f);    // cyan accent
    private Color _cGreen = new Color(0.165f, 0.878f, 0.533f, 1f);     // green
    private Color _cWhite = new Color(0.92f, 0.97f, 1f, 1f);           // white
    private Color _cMuted = new Color(0.5f, 0.65f, 0.72f, 1f);         // muted text

    [Header("Synced State")]
    [UdonSynced] private int[] _bannedIds = new int[40];
    [UdonSynced] private int[] _frozenIds = new int[40];
    [UdonSynced] private int[] _moderatorIds = new int[20];
    [UdonSynced] private int _bannedCount = 0;
    [UdonSynced] private int _frozenCount = 0;
    [UdonSynced] private int _moderatorCount = 0;
    [UdonSynced] private string _lastAction = "";
    [UdonSynced] private string[] _modLog = new string[8];
    [UdonSynced] private int _modLogCount = 0;
    [UdonSynced] private int _sizeTargetPlayerId = -1;
    [UdonSynced] private float _sizeTargetHeight = 1.8f;
    [UdonSynced] private int _bringTargetId = -1;
    [UdonSynced] private Vector3 _bringPosition = Vector3.zero;
    [UdonSynced] private Quaternion _bringRotation = Quaternion.identity;
    [UdonSynced] private int _speedTargetPlayerId = -1;
    [UdonSynced] private float _speedWalk = 2f;
    [UdonSynced] private float _speedRun = 4f;
    [UdonSynced] private float _speedJump = 3f;
    [UdonSynced] private int _voiceMode = 0; // 0=normal, 1=muted, 2=worldwide

    [Header("Internal State")]
    private bool _isAdmin = false;
    private bool _isModerator = false;
    private int _selectedPlayerId = -1;
    private VRCPlayerApi _selectedPlayer = null;
    private bool _uiReady = false;
    private bool _panelVisible = false;
    private int _currentTab = 0; // 0=Players, 1=Actions, 2=Move, 3=Sound, 4=Log

    // Tab UI
    private Button[] _tabButtons = new Button[5];
    private GameObject[] _tabContents = new GameObject[5];
    private Image[] _tabButtonImages = new Image[5];

    // UI References
    private Text _titleText;
    private Text _statusText;
    private Text _selectedNameText;
    private Text _selectedRoleText;
    private Text _playerListText;
    private Text _actionLogText;
    private GameObject _panel;
    private GameObject _toggleBtn;
    private Button _teleportToBtn;
    private Button _bringBtn;
    private Button _freezeBtn;
    private Button _muteBtn;
    private Button _banBtn;
    private Button _unbanBtn;
    private Button _promoteBtn;
    private Button _demoteBtn;
    private Button _closeBtn;
    private Button _muteAllBtn;
    private Button _unmuteAllBtn;
    private Button _voiceWorldwideBtn;
    private Button _resetVoiceBtn;
    private Button _resetCrackBtn;
    private Button _resetTriviaBtn;
    private Button _speedSlowBtn;
    private Button _speedNormalBtn;
    private Button _speedFastBtn;
    private Button _sizeSmallBtn;
    private Button _sizeNormalBtn;
    private Button _sizeLargeBtn;
    private Button _sizeGiantBtn;
    private Image _selectedBg;
    private Text[] _logLineTexts = new Text[8];

    // Cross-game targets (found at runtime by scene path, not Inspector-wired)
    private UdonBehaviour _crackTheCodeTarget;
    private UdonBehaviour _triviaTarget;

    // Player list rows
    private Text[] _rowNameTexts = new Text[8];
    private Image[] _rowBgImages = new Image[8];
    private Button[] _rowButtons = new Button[8];
    private int _listScrollOffset = 0;

    // Animation
    private float _animTimer = 0f;
    private int _animPhase = 0; // 0=idle, 1=slide-in, 2=slide-out, 3=action-flash, 4=pulse
    private float _slideProgress = 0f;
    private float _flashTimer = 0f;
    private float _pulseTimer = 0f;
    private Vector3 _panelTargetPos = Vector3.zero;
    private Vector3 _panelHiddenPos = Vector3.zero;
    private bool _flashOn = false;

    // Player tracking
    private VRCPlayerApi[] _allPlayers = new VRCPlayerApi[40];
    private int _playerCount = 0;
    private float _refreshTimer = 0f;

    // Input handling for summoning the mod tool
    private Transform _canvasTransform;
    private float _lastLeftTriggerTime = -1f;
    private const float _doubleTriggerWindow = 0.4f; // seconds between presses
    private bool _prevJKey = false;
    private bool _prevLeftTrigger = false;

    // Mute tracking (local only)
    private int[] _mutedIds = new int[40];
    private int _mutedCount = 0;

    // Local tracking of which players we've frozen/banned (to detect removals)
    private int[] _locallyFrozenIds = new int[40];
    private int _locallyFrozenCount = 0;
    private int[] _locallyBannedIds = new int[40];
    private int _locallyBannedCount = 0;

    private void Start()
    {
        FindUi();
        CheckAdminStatus();
        ApplyState(true);
        Debug.Log("[ChromixModTool] Start() called. uiReady=" + _uiReady + " isAdmin=" + _isAdmin);
    }

    public override void OnPlayerJoined(VRCPlayerApi player)
    {
        RefreshPlayerList();
        CheckAdminStatus();
        // Re-apply frozen/banned state so the new player sees existing punishments.
        NetApplyFrozenState();
        NetApplyBannedState();
        ApplyState(false);
    }

    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        RefreshPlayerList();
        if (_selectedPlayerId == player.playerId)
        {
            _selectedPlayerId = -1;
            _selectedPlayer = null;
        }
        ApplyState(false);
    }

    public override void OnDeserialization()
    {
        // Apply synced frozen/banned state on all clients so punishments
        // take effect for everyone, not just the mod who issued them.
        NetApplyFrozenState();
        NetApplyBannedState();
        NetApplyMutedState();
        NetApplySize();
        NetApplySpeed();
        NetApplyBring();
        ApplyState(false);
    }

    private void Update()
    {
        if (!_uiReady) return;

        // Input handling — only admins can summon
        if (_isAdmin || _isModerator)
        {
            HandleSummonInput();
        }

        // Refresh player list periodically
        _refreshTimer += Time.deltaTime;
        if (_refreshTimer > 2f)
        {
            _refreshTimer = 0f;
            RefreshPlayerList();
            UpdatePlayerListUI();
        }

        // Animation updates
        UpdateAnimations();
    }

    /// <summary>
    /// Detects double-press of left hand trigger (VR) or J key (desktop)
    /// to summon the mod tool panel facing the player.
    /// </summary>
    private void HandleSummonInput()
    {
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null) return;

        bool summonRequested = false;

        // Desktop: J key press (edge-triggered)
        bool jKey = Input.GetKey(KeyCode.J);
        if (jKey && !_prevJKey)
        {
            summonRequested = true;
        }
        _prevJKey = jKey;

        // VR: Left hand trigger double-press (edge-triggered)
        // Secondary = left hand in VRChat's Oculus CrossPlatform input mapping
        float leftTrigger = Input.GetAxisRaw("Oculus_CrossPlatform_SecondaryIndexTrigger");
        bool leftTriggerDown = leftTrigger > 0.5f;
        if (leftTriggerDown && !_prevLeftTrigger)
        {
            float now = Time.time;
            if (now - _lastLeftTriggerTime < _doubleTriggerWindow)
            {
                summonRequested = true;
                _lastLeftTriggerTime = -1f; // reset so triple-press doesn't re-trigger
            }
            else
            {
                _lastLeftTriggerTime = now;
            }
        }
        _prevLeftTrigger = leftTriggerDown;

        if (summonRequested)
        {
            if (!_panelVisible)
            {
                // Enable canvas locally — only this client can see it
                if (_canvasTransform != null) _canvasTransform.gameObject.SetActive(true);
                PositionPanelFacingPlayer(local);
                TogglePanel();
            }
            else
            {
                // Already visible — close it
                ClosePanel();
            }
        }
    }

    /// <summary>
    /// Positions the canvas in front of the player, facing them.
    /// </summary>
    private void PositionPanelFacingPlayer(VRCPlayerApi player)
    {
        if (_canvasTransform == null) return;

        // Use head tracking data for accurate eye height and gaze direction
        VRCPlayerApi.TrackingData headData = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        Vector3 headPos = headData.position;
        Quaternion headRot = headData.rotation;
        Vector3 forward = headRot * Vector3.forward;
        forward.y = 0f; // keep panel level (don't tilt up/down with head pitch)
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        forward = forward.normalized;
        Vector3 up = Vector3.up;

        // Place 1.0m in front of head, centered at head height
        Vector3 targetPos = headPos + forward * 1.0f;

        // Face the player (panel forward points back at the player)
        Quaternion targetRot = Quaternion.LookRotation(forward, up);

        _canvasTransform.position = targetPos;
        _canvasTransform.rotation = targetRot;
    }

    // ==================== UI Setup ====================

    private Text FindText(Transform parent, string name)
    {
        if (parent == null) return null;
        Transform t = parent.Find(name);
        if (t == null) return null;
        return t.GetComponent<Text>();
    }

    private Button FindButton(Transform parent, string name)
    {
        if (parent == null) return null;
        Transform t = parent.Find(name);
        if (t == null) return null;
        return t.GetComponent<Button>();
    }

    private void FindUi()
    {
        Transform assetRoot = transform.parent;
        RectTransform canvas = assetRoot != null ? (RectTransform)assetRoot.Find("UI_Canvas") : null;
        if (canvas == null)
        {
            Debug.LogError("[ChromixModTool] UI_Canvas not found under " + (assetRoot != null ? assetRoot.name : "null"));
            return;
        }

        _canvasTransform = canvas;
        _panel = canvas.Find("Panel").gameObject;
        _toggleBtn = null; // No toggle button in tabbed UI

        Transform panelT = _panel.transform;

        // Header
        Transform header = panelT.Find("Header");
        if (header != null)
        {
            _titleText = FindText(header, "Title");
            _closeBtn = FindButton(header, "CloseBtn");
        }

        // Tab bar
        Transform tabBar = panelT.Find("TabBar");
        if (tabBar != null)
        {
            for (int i = 0; i < 5; i++)
            {
                Transform tbt = tabBar.Find("TabBtn" + i);
                if (tbt != null)
                {
                    _tabButtons[i] = tbt.GetComponent<Button>();
                    _tabButtonImages[i] = tbt.GetComponent<Image>();
                }
            }
        }

        // Tab contents
        string[] tabNames = { "Tab_Players", "Tab_Actions", "Tab_Move", "Tab_Sound", "Tab_Log" };
        for (int i = 0; i < 5; i++)
        {
            Transform tabTr = panelT.Find(tabNames[i]);
            _tabContents[i] = tabTr != null ? tabTr.gameObject : null;
            if (_tabContents[i] != null) _tabContents[i].SetActive(i == 0);
        }

        // Tab 0: Players
        Transform tp = panelT.Find("Tab_Players");
        if (tp != null)
        {
            Transform si = tp.Find("SelectedInfo");
            if (si != null)
            {
                _selectedNameText = FindText(si, "NameText");
                _selectedRoleText = FindText(si, "RoleText");
                _selectedBg = si.GetComponent<Image>();
            }

            Transform pl = tp.Find("PlayerList");
            if (pl != null)
            {
                _playerListText = FindText(pl, "ListText");
                for (int i = 0; i < 8; i++)
                {
                    Transform row = pl.Find("Row" + i);
                    if (row != null)
                    {
                        _rowNameTexts[i] = FindText(row, "NameText");
                        _rowBgImages[i] = row.GetComponent<Image>();
                        _rowButtons[i] = row.GetComponent<Button>();
                    }
                }
            }
        }

        // Tab 1: Actions
        Transform ta = panelT.Find("Tab_Actions");
        if (ta != null)
        {
            _teleportToBtn = FindButton(ta, "TeleportToBtn");
            _bringBtn = FindButton(ta, "BringBtn");
            _freezeBtn = FindButton(ta, "FreezeBtn");
            _muteBtn = FindButton(ta, "MuteBtn");
            _banBtn = FindButton(ta, "BanBtn");
            _unbanBtn = FindButton(ta, "UnbanBtn");
            _promoteBtn = FindButton(ta, "PromoteBtn");
            _demoteBtn = FindButton(ta, "DemoteBtn");

            Transform al = ta.Find("ActionLog");
            if (al != null) _actionLogText = FindText(al, "LogText");
        }

        // Tab 2: Move
        Transform tm = panelT.Find("Tab_Move");
        if (tm != null)
        {
            _speedSlowBtn = FindButton(tm, "SpeedSlowBtn");
            _speedNormalBtn = FindButton(tm, "SpeedNormalBtn");
            _speedFastBtn = FindButton(tm, "SpeedFastBtn");
            _sizeSmallBtn = FindButton(tm, "SizeSmallBtn");
            _sizeNormalBtn = FindButton(tm, "SizeNormalBtn");
            _sizeLargeBtn = FindButton(tm, "SizeLargeBtn");
            _sizeGiantBtn = FindButton(tm, "SizeGiantBtn");
        }

        // Tab 3: Sound
        Transform ts = panelT.Find("Tab_Sound");
        if (ts != null)
        {
            _muteAllBtn = FindButton(ts, "MuteAllBtn");
            _unmuteAllBtn = FindButton(ts, "UnmuteAllBtn");
            _voiceWorldwideBtn = FindButton(ts, "VoiceWorldwideBtn");
            _resetCrackBtn = FindButton(ts, "ResetCrackBtn");
            _resetTriviaBtn = FindButton(ts, "ResetTriviaBtn");
            _resetVoiceBtn = FindButton(ts, "ResetVoiceBtn");
        }

        // Tab 4: Log
        Transform tl = panelT.Find("Tab_Log");
        if (tl != null)
        {
            for (int i = 0; i < 8; i++)
                _logLineTexts[i] = FindText(tl, "Log" + i);
        }

        // Cross-game targets
        GameObject ctcObj = GameObject.Find("CrackTheCode/CrackTheCode_Asset/GameSystems");
        if (ctcObj != null) _crackTheCodeTarget = ctcObj.GetComponent<UdonBehaviour>();

        GameObject triviaObj = GameObject.Find("ChromixTrivia/ChromixTrivia_Asset/TriviaSystems");
        if (triviaObj != null) _triviaTarget = triviaObj.GetComponent<UdonBehaviour>();

        // Panel animation setup — disable entire canvas so no one else can see it
        if (_panel != null)
        {
            RectTransform pr = _panel.GetComponent<RectTransform>();
            _panelTargetPos = pr.anchoredPosition;
            _panelHiddenPos = _panelTargetPos + new Vector3(500f, 0f, 0f);
            pr.anchoredPosition = _panelHiddenPos;
            _panel.SetActive(false);
        }
        if (_canvasTransform != null)
        {
            _canvasTransform.gameObject.SetActive(false);
        }

        // Set initial tab
        UpdateTabVisuals();

        _uiReady = true;
    }

    // ==================== Tab Switching ====================

    public void SwitchTab0() { SwitchTab(0); }
    public void SwitchTab1() { SwitchTab(1); }
    public void SwitchTab2() { SwitchTab(2); }
    public void SwitchTab3() { SwitchTab(3); }
    public void SwitchTab4() { SwitchTab(4); }

    private void SwitchTab(int tab)
    {
        if (tab < 0 || tab >= 5) return;
        _currentTab = tab;
        for (int i = 0; i < 5; i++)
        {
            if (_tabContents[i] != null) _tabContents[i].SetActive(i == tab);
        }
        UpdateTabVisuals();
        ApplyState(false);
    }

    private void UpdateTabVisuals()
    {
        Color activeColor = new Color(0.157f, 0.847f, 0.961f, 0.25f);
        Color inactiveColor = new Color(0.06f, 0.12f, 0.18f, 0.98f);
        for (int i = 0; i < 5; i++)
        {
            if (_tabButtonImages[i] != null)
                _tabButtonImages[i].color = (i == _currentTab) ? activeColor : inactiveColor;
        }
    }

    // ==================== Admin System ====================

    private void CheckAdminStatus()
    {
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null) return;

        // Admin = hardcoded display name OR instance master
        _isAdmin = IsHardcodedAdmin(local.displayName) || local.isMaster;

        // Check if in moderator list
        _isModerator = IsModerator(local.playerId);

        // Toggle button is always hidden now — admins summon via input
        // (_toggleBtn is null in the new tabbed UI, no need to hide it)
    }

    private bool IsHardcodedAdmin(string displayName)
    {
        if (string.IsNullOrEmpty(displayName)) return false;
        foreach (string admin in _hardcodedAdmins)
        {
            if (displayName == admin) return true;
        }
        return false;
    }

    private bool IsModerator(int playerId)
    {
        for (int i = 0; i < _moderatorCount; i++)
        {
            if (_moderatorIds[i] == playerId) return true;
        }
        return false;
    }

    private bool IsBanned(int playerId)
    {
        for (int i = 0; i < _bannedCount; i++)
        {
            if (_bannedIds[i] == playerId) return true;
        }
        return false;
    }

    private bool IsFrozen(int playerId)
    {
        for (int i = 0; i < _frozenCount; i++)
        {
            if (_frozenIds[i] == playerId) return true;
        }
        return false;
    }

    private bool IsMuted(int playerId)
    {
        for (int i = 0; i < _mutedCount; i++)
        {
            if (_mutedIds[i] == playerId) return true;
        }
        return false;
    }

    private void AddModerator(int playerId)
    {
        if (_moderatorCount >= 20) return;
        if (IsModerator(playerId)) return;
        _moderatorIds[_moderatorCount] = playerId;
        _moderatorCount++;
    }

    private void RemoveModerator(int playerId)
    {
        for (int i = 0; i < _moderatorCount; i++)
        {
            if (_moderatorIds[i] == playerId)
            {
                _moderatorIds[i] = _moderatorIds[_moderatorCount - 1];
                _moderatorIds[_moderatorCount - 1] = 0;
                _moderatorCount--;
                return;
            }
        }
    }

    private void AddBanned(int playerId)
    {
        if (_bannedCount >= 40) return;
        if (IsBanned(playerId)) return;
        _bannedIds[_bannedCount] = playerId;
        _bannedCount++;
    }

    private void RemoveBanned(int playerId)
    {
        for (int i = 0; i < _bannedCount; i++)
        {
            if (_bannedIds[i] == playerId)
            {
                _bannedIds[i] = _bannedIds[_bannedCount - 1];
                _bannedIds[_bannedCount - 1] = 0;
                _bannedCount--;
                return;
            }
        }
    }

    private void AddFrozen(int playerId)
    {
        if (_frozenCount >= 40) return;
        if (IsFrozen(playerId)) return;
        _frozenIds[_frozenCount] = playerId;
        _frozenCount++;
    }

    private void RemoveFrozen(int playerId)
    {
        for (int i = 0; i < _frozenCount; i++)
        {
            if (_frozenIds[i] == playerId)
            {
                _frozenIds[i] = _frozenIds[_frozenCount - 1];
                _frozenIds[_frozenCount - 1] = 0;
                _frozenCount--;
                return;
            }
        }
    }

    private void AddMuted(int playerId)
    {
        if (_mutedCount >= 40) return;
        if (IsMuted(playerId)) return;
        _mutedIds[_mutedCount] = playerId;
        _mutedCount++;
    }

    private void RemoveMuted(int playerId)
    {
        for (int i = 0; i < _mutedCount; i++)
        {
            if (_mutedIds[i] == playerId)
            {
                _mutedIds[i] = _mutedIds[_mutedCount - 1];
                _mutedIds[_mutedCount - 1] = 0;
                _mutedCount--;
                return;
            }
        }
    }

    // ==================== Player List ====================

    private void RefreshPlayerList()
    {
        _playerCount = VRCPlayerApi.GetPlayerCount();
        if (_playerCount > 40) _playerCount = 40;
        VRCPlayerApi[] temp = new VRCPlayerApi[40];
        VRCPlayerApi.GetPlayers(temp);
        for (int i = 0; i < _playerCount; i++)
        {
            _allPlayers[i] = temp[i];
        }
    }

    private string GetRoleName(VRCPlayerApi player)
    {
        if (player == null) return "";
        if (player.isMaster) return "ADMIN";
        if (IsModerator(player.playerId)) return "MOD";
        if (IsBanned(player.playerId)) return "BANNED";
        return "PLAYER";
    }

    private Color GetRoleColor(VRCPlayerApi player)
    {
        if (player == null) return _cPlayer;
        if (player.isMaster) return _cAdmin;
        if (IsModerator(player.playerId)) return _cModerator;
        if (IsBanned(player.playerId)) return _cBanned;
        return _cPlayer;
    }

    private void UpdatePlayerListUI()
    {
        if (!_uiReady) return;

        int visibleCount = Mathf.Min(8, _playerCount - _listScrollOffset);
        for (int i = 0; i < 8; i++)
        {
            if (i < visibleCount)
            {
                int idx = _listScrollOffset + i;
                VRCPlayerApi p = _allPlayers[idx];
                if (p == null) continue;

                if (_rowNameTexts[i] != null)
                {
                    string name = p.displayName;
                    if (name.Length > 18) name = name.Substring(0, 16) + "..";
                    string role = GetRoleName(p);
                    string prefix = "";
                    if (IsFrozen(p.playerId)) prefix += "[F] ";
                    if (IsMuted(p.playerId)) prefix += "[M] ";
                    _rowNameTexts[i].text = prefix + name;
                    _rowNameTexts[i].color = GetRoleColor(p);
                }

                if (_rowBgImages[i] != null)
                {
                    if (_selectedPlayerId == p.playerId)
                    {
                        _rowBgImages[i].color = new Color(_cSelected.r, _cSelected.g, _cSelected.b, 0.15f);
                    }
                    else
                    {
                        _rowBgImages[i].color = _cDim;
                    }
                }

                if (_rowButtons[i] != null)
                {
                    _rowButtons[i].gameObject.SetActive(true);
                }
            }
            else
            {
                if (_rowNameTexts[i] != null) _rowNameTexts[i].text = "";
                if (_rowButtons[i] != null) _rowButtons[i].gameObject.SetActive(false);
            }
        }
    }

    // ==================== State Application ====================

    private void ApplyState(bool initial)
    {
        if (!_uiReady) return;

        // Update selected player info
        if (_selectedPlayer != null && _selectedPlayer.IsValid())
        {
            if (_selectedNameText != null)
            {
                _selectedNameText.text = _selectedPlayer.displayName;
                _selectedNameText.color = GetRoleColor(_selectedPlayer);
            }
            if (_selectedRoleText != null)
            {
                _selectedRoleText.text = GetRoleName(_selectedPlayer);
                _selectedRoleText.color = GetRoleColor(_selectedPlayer);
            }

            // Update action button states
            bool isBanned = IsBanned(_selectedPlayer.playerId);
            bool isFrozen = IsFrozen(_selectedPlayer.playerId);
            bool isMod = IsModerator(_selectedPlayer.playerId);
            bool isMuted = IsMuted(_selectedPlayer.playerId);

            if (_banBtn != null) _banBtn.gameObject.SetActive(!isBanned);
            if (_unbanBtn != null) _unbanBtn.gameObject.SetActive(isBanned);
            if (_freezeBtn != null)
            {
                _freezeBtn.GetComponentInChildren<Text>().text = isFrozen ? "UNFREEZE" : "FREEZE";
            }
            if (_muteBtn != null)
            {
                _muteBtn.GetComponentInChildren<Text>().text = isMuted ? "UNMUTE" : "MUTE";
            }
            if (_promoteBtn != null) _promoteBtn.gameObject.SetActive(!isMod && !_selectedPlayer.isMaster);
            if (_demoteBtn != null) _demoteBtn.gameObject.SetActive(isMod);

            // Speed buttons — visible when a player is selected and not frozen
            bool showSpeed = !isFrozen;
            if (_speedSlowBtn != null) _speedSlowBtn.gameObject.SetActive(showSpeed);
            if (_speedNormalBtn != null) _speedNormalBtn.gameObject.SetActive(showSpeed);
            if (_speedFastBtn != null) _speedFastBtn.gameObject.SetActive(showSpeed);

            // Size buttons — visible when a player is selected
            if (_sizeSmallBtn != null) _sizeSmallBtn.gameObject.SetActive(true);
            if (_sizeNormalBtn != null) _sizeNormalBtn.gameObject.SetActive(true);
            if (_sizeLargeBtn != null) _sizeLargeBtn.gameObject.SetActive(true);
            if (_sizeGiantBtn != null) _sizeGiantBtn.gameObject.SetActive(true);
        }
        else
        {
            if (_selectedNameText != null) _selectedNameText.text = "No player selected";
            if (_selectedRoleText != null) _selectedRoleText.text = "";
            if (_banBtn != null) _banBtn.gameObject.SetActive(false);
            if (_unbanBtn != null) _unbanBtn.gameObject.SetActive(false);
            if (_freezeBtn != null) _freezeBtn.gameObject.SetActive(false);
            if (_muteBtn != null) _muteBtn.gameObject.SetActive(false);
            if (_promoteBtn != null) _promoteBtn.gameObject.SetActive(false);
            if (_demoteBtn != null) _demoteBtn.gameObject.SetActive(false);
            if (_speedSlowBtn != null) _speedSlowBtn.gameObject.SetActive(false);
            if (_speedNormalBtn != null) _speedNormalBtn.gameObject.SetActive(false);
            if (_speedFastBtn != null) _speedFastBtn.gameObject.SetActive(false);
            if (_sizeSmallBtn != null) _sizeSmallBtn.gameObject.SetActive(false);
            if (_sizeNormalBtn != null) _sizeNormalBtn.gameObject.SetActive(false);
            if (_sizeLargeBtn != null) _sizeLargeBtn.gameObject.SetActive(false);
            if (_sizeGiantBtn != null) _sizeGiantBtn.gameObject.SetActive(false);
        }

        // Update status
        if (_statusText != null)
        {
            if (_isAdmin) _statusText.text = "ADMIN MODE";
            else if (_isModerator) _statusText.text = "MODERATOR MODE";
            else _statusText.text = "VIEW ONLY";
        }

        // Update action log (single-line recent action)
        if (_actionLogText != null && _lastAction != "")
        {
            _actionLogText.text = _lastAction;
        }

        // Update analytics board (multi-line mod log)
        for (int i = 0; i < 8; i++)
        {
            if (_logLineTexts[i] == null) continue;
            if (i < _modLogCount)
            {
                _logLineTexts[i].text = _modLog[i];
                _logLineTexts[i].color = _cMuted;
            }
            else
            {
                _logLineTexts[i].text = "";
            }
        }

        UpdatePlayerListUI();
    }

    // ==================== Animation ====================

    private void UpdateAnimations()
    {
        if (_animPhase == 1) // slide-in
        {
            _slideProgress += Time.deltaTime * 4f;
            if (_slideProgress >= 1f)
            {
                _slideProgress = 1f;
                _animPhase = 0;
                if (_panel != null)
                    _panel.GetComponent<RectTransform>().anchoredPosition = _panelTargetPos;
            }
            else
            {
                float t = EaseOutCubic(_slideProgress);
                if (_panel != null)
                    _panel.GetComponent<RectTransform>().anchoredPosition =
                        Vector3.Lerp(_panelHiddenPos, _panelTargetPos, t);
            }
        }
        else if (_animPhase == 2) // slide-out
        {
            _slideProgress += Time.deltaTime * 4f;
            if (_slideProgress >= 1f)
            {
                _slideProgress = 1f;
                _animPhase = 0;
                if (_panel != null)
                {
                    _panel.GetComponent<RectTransform>().anchoredPosition = _panelHiddenPos;
                    _panel.SetActive(false);
                }
                // Disable entire canvas so nothing is visible to anyone
                if (_canvasTransform != null) _canvasTransform.gameObject.SetActive(false);
                _panelVisible = false;
            }
            else
            {
                float t = EaseInCubic(_slideProgress);
                if (_panel != null)
                    _panel.GetComponent<RectTransform>().anchoredPosition =
                        Vector3.Lerp(_panelTargetPos, _panelHiddenPos, t);
            }
        }

        // Action flash
        if (_flashTimer > 0f)
        {
            _flashTimer -= Time.deltaTime;
            _flashOn = !_flashOn;
            if (_selectedBg != null)
            {
                if (_flashOn)
                    _selectedBg.color = new Color(_cAccent.r, _cAccent.g, _cAccent.b, 0.3f);
                else
                    _selectedBg.color = _cNormal;
            }
            if (_flashTimer <= 0f)
            {
                if (_selectedBg != null) _selectedBg.color = _cNormal;
            }
        }

        // Pulse on selected row
        _pulseTimer += Time.deltaTime;
        if (_pulseTimer > 0.8f) _pulseTimer = 0f;
        float pulse = Mathf.Sin(_pulseTimer * Mathf.PI / 0.8f) * 0.5f + 0.5f;
        for (int i = 0; i < 8; i++)
        {
            if (_rowBgImages[i] != null && _rowNameTexts[i] != null)
            {
                int idx = _listScrollOffset + i;
                if (idx < _playerCount && _allPlayers[idx] != null &&
                    _selectedPlayerId == _allPlayers[idx].playerId)
                {
                    _rowBgImages[i].color = new Color(_cSelected.r, _cSelected.g, _cSelected.b, 0.1f + pulse * 0.15f);
                }
            }
        }
    }

    private float EaseOutCubic(float t) { return 1f - Mathf.Pow(1f - t, 3f); }
    private float EaseInCubic(float t) { return t * t * t; }

    private void TriggerFlash()
    {
        _flashTimer = 0.6f;
        _flashOn = true;
    }

    // ==================== Public Button Events ====================

    public void TogglePanel()
    {
        if (!_uiReady) return;
        if (!_isAdmin && !_isModerator) return;

        if (_panelVisible)
        {
            _animPhase = 2;
            _slideProgress = 0f;
        }
        else
        {
            _panel.SetActive(true);
            _panelVisible = true;
            _animPhase = 1;
            _slideProgress = 0f;
            RefreshPlayerList();
            ApplyState(false);
        }
    }

    public void ClosePanel()
    {
        if (_panelVisible)
        {
            _animPhase = 2;
            _slideProgress = 0f;
        }
    }

    public void SelectRow0() { SelectRow(0); }
    public void SelectRow1() { SelectRow(1); }
    public void SelectRow2() { SelectRow(2); }
    public void SelectRow3() { SelectRow(3); }
    public void SelectRow4() { SelectRow(4); }
    public void SelectRow5() { SelectRow(5); }
    public void SelectRow6() { SelectRow(6); }
    public void SelectRow7() { SelectRow(7); }

    private void SelectRow(int row)
    {
        int idx = _listScrollOffset + row;
        if (idx >= _playerCount) return;
        VRCPlayerApi p = _allPlayers[idx];
        if (p == null) return;

        _selectedPlayerId = p.playerId;
        _selectedPlayer = p;
        TriggerFlash();
        ApplyState(false);
    }

    public void ScrollUp()
    {
        if (_listScrollOffset > 0)
        {
            _listScrollOffset--;
            UpdatePlayerListUI();
        }
    }

    public void ScrollDown()
    {
        if (_listScrollOffset + 8 < _playerCount)
        {
            _listScrollOffset++;
            UpdatePlayerListUI();
        }
    }

    // TeleportToPlayer is local-only — teleports yourself to the target, no sync needed
    public void TeleportToPlayer()
    {
        if (!CanModerate() || _selectedPlayer == null || !_selectedPlayer.IsValid()) return;
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null) return;

        Vector3 targetPos = _selectedPlayer.GetPosition();
        Quaternion targetRot = _selectedPlayer.GetRotation();
        local.TeleportTo(targetPos, targetRot);
        LogAction(local.displayName + " teleported to " + _selectedPlayer.displayName);
        TriggerFlash();
        ApplyState(false);
    }

    // BringPlayer uses synced fields + network event to owner
    public void BringPlayer()
    {
        if (!CanModerate() || _selectedPlayer == null || !_selectedPlayer.IsValid()) return;
        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "NetBringPlayer");
    }

    public void NetBringPlayer()
    {
        if (!Networking.IsOwner(gameObject)) return;
        if (_selectedPlayer == null || !_selectedPlayer.IsValid()) return;
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null) return;

        Vector3 myPos = local.GetPosition() + local.GetRotation() * Vector3.forward * 2f;
        Quaternion myRot = local.GetRotation();
        _bringTargetId = _selectedPlayer.playerId;
        _bringPosition = myPos;
        _bringRotation = myRot;
        LogAction(local.displayName + " brought " + _selectedPlayer.displayName);
        RequestSerialization();
        TriggerFlash();
        ApplyState(false);
    }

    public void NetApplyBring()
    {
        if (_bringTargetId < 0) return;
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null) return;
        if (local.playerId != _bringTargetId) return;
        local.TeleportTo(_bringPosition, _bringRotation);
        _bringTargetId = -1;
    }

    // ==================== Freeze / Unfreeze ====================

    public void ToggleFreeze()
    {
        if (_selectedPlayer == null || !_selectedPlayer.IsValid()) return;
        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "NetToggleFreeze");
    }

    public void NetToggleFreeze()
    {
        if (!Networking.IsOwner(gameObject)) return;
        if (_selectedPlayer == null || !_selectedPlayer.IsValid()) return;
        if (IsFrozen(_selectedPlayer.playerId))
        {
            RemoveFrozen(_selectedPlayer.playerId);
            LogAction(Networking.LocalPlayer.displayName + " unfroze " + _selectedPlayer.displayName);
        }
        else
        {
            AddFrozen(_selectedPlayer.playerId);
            LogAction(Networking.LocalPlayer.displayName + " froze " + _selectedPlayer.displayName);
        }
        // Immobilize/Speed applied by each client in NetApplyFrozenState (local player only)
        RequestSerialization();
        TriggerFlash();
        ApplyState(false);
    }

    // ==================== Speed Control ====================

    public void SpeedSlow() { SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "NetSpeedSlow"); }
    public void SpeedNormal() { SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "NetSpeedNormal"); }
    public void SpeedFast() { SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "NetSpeedFast"); }

    public void NetSpeedSlow() { OwnerSetSpeed(1f, 2f, 2f, "SLOW"); }
    public void NetSpeedNormal() { OwnerSetSpeed(2f, 4f, 3f, "NORMAL"); }
    public void NetSpeedFast() { OwnerSetSpeed(5f, 10f, 6f, "FAST"); }

    private void OwnerSetSpeed(float walk, float run, float jump, string label)
    {
        if (!Networking.IsOwner(gameObject)) return;
        if (_selectedPlayer == null || !_selectedPlayer.IsValid()) return;
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null) return;

        // Speed only works on the local player — sync target + values, each client applies
        _speedTargetPlayerId = _selectedPlayer.playerId;
        _speedWalk = walk;
        _speedRun = run;
        _speedJump = jump;
        LogAction(local.displayName + " set " + _selectedPlayer.displayName + " speed to " + label);
        RequestSerialization();
        TriggerFlash();
        ApplyState(false);
    }

    // ==================== Size Control ====================

    /// <summary>Called from OnDeserialization — applies size if this client is the target.</summary>
    private void NetApplySize()
    {
        if (_sizeTargetPlayerId < 0) return;
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null) return;
        if (local.playerId == _sizeTargetPlayerId)
        {
            local.SetAvatarEyeHeightByMeters(_sizeTargetHeight);
        }
    }

    /// <summary>Called from OnDeserialization — applies speed if this client is the target.</summary>
    private void NetApplySpeed()
    {
        if (_speedTargetPlayerId < 0) return;
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null) return;
        if (local.playerId == _speedTargetPlayerId)
        {
            local.SetWalkSpeed(_speedWalk);
            local.SetRunSpeed(_speedRun);
            local.SetStrafeSpeed(_speedWalk);
            local.SetJumpImpulse(_speedJump);
        }
    }

    public void SizeSmall() { SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "NetSizeSmall"); }
    public void SizeNormal() { SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "NetSizeNormal"); }
    public void SizeLarge() { SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "NetSizeLarge"); }
    public void SizeGiant() { SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "NetSizeGiant"); }

    public void NetSizeSmall() { OwnerSetSize(0.8f, "SMALL"); }
    public void NetSizeNormal() { OwnerSetSize(1.8f, "NORMAL"); }
    public void NetSizeLarge() { OwnerSetSize(3.0f, "LARGE"); }
    public void NetSizeGiant() { OwnerSetSize(5.0f, "GIANT"); }

    private void OwnerSetSize(float eyeHeightMeters, string label)
    {
        if (!Networking.IsOwner(gameObject)) return;
        if (_selectedPlayer == null || !_selectedPlayer.IsValid()) return;
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null) return;

        _sizeTargetPlayerId = _selectedPlayer.playerId;
        _sizeTargetHeight = eyeHeightMeters;
        LogAction(local.displayName + " set " + _selectedPlayer.displayName + " size to " + label);
        RequestSerialization();
        TriggerFlash();
        ApplyState(false);

        // Apply locally immediately if the mod is targeting themselves
        if (_selectedPlayer.playerId == local.playerId)
        {
            local.SetAvatarEyeHeightByMeters(eyeHeightMeters);
        }
    }

    // ==================== Mute All / Unmute All ====================

    public void MuteAll() { SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "NetMuteAll"); }
    public void UnmuteAll() { SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "NetUnmuteAll"); }

    public void NetMuteAll()
    {
        if (!Networking.IsOwner(gameObject) || !CanModerate()) return;
        VRCPlayerApi local = Networking.LocalPlayer;
        RefreshPlayerList();
        int count = 0;
        for (int i = 0; i < _playerCount; i++)
        {
            VRCPlayerApi p = _allPlayers[i];
            if (p == null || !p.IsValid()) continue;
            if (p.isMaster) continue;
            if (!IsMuted(p.playerId)) AddMuted(p.playerId);
            count++;
        }
        LogAction(local.displayName + " muted all (" + count + " players)");
        _voiceMode = 1;
        // Voice settings applied by each client in NetApplyMutedState
        RequestSerialization();
        TriggerFlash();
        ApplyState(false);
    }

    public void NetUnmuteAll()
    {
        if (!Networking.IsOwner(gameObject) || !CanModerate()) return;
        VRCPlayerApi local = Networking.LocalPlayer;
        RefreshPlayerList();
        for (int i = 0; i < _playerCount; i++)
        {
            VRCPlayerApi p = _allPlayers[i];
            if (p == null || !p.IsValid()) continue;
            if (IsMuted(p.playerId)) RemoveMuted(p.playerId);
        }
        LogAction(local.displayName + " unmuted all");
        _voiceMode = 0;
        // Voice settings applied by each client in NetApplyMutedState
        RequestSerialization();
        TriggerFlash();
        ApplyState(false);
    }

    // ==================== Voice Worldwide / Reset Voice ====================

    public void VoiceWorldwide() { SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "NetVoiceWorldwide"); }
    public void ResetVoice() { SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "NetResetVoice"); }

    public void NetVoiceWorldwide()
    {
        if (!Networking.IsOwner(gameObject) || !CanModerate()) return;
        VRCPlayerApi local = Networking.LocalPlayer;
        RefreshPlayerList();
        for (int i = 0; i < _playerCount; i++)
        {
            VRCPlayerApi p = _allPlayers[i];
            if (p == null || !p.IsValid()) continue;
            if (IsMuted(p.playerId)) RemoveMuted(p.playerId);
        }
        _voiceMode = 2;
        LogAction(local.displayName + " set voice worldwide");
        // Voice settings applied by each client in NetApplyMutedState
        RequestSerialization();
        TriggerFlash();
        ApplyState(false);
    }

    public void NetResetVoice()
    {
        if (!Networking.IsOwner(gameObject) || !CanModerate()) return;
        VRCPlayerApi local = Networking.LocalPlayer;
        RefreshPlayerList();
        for (int i = 0; i < _playerCount; i++)
        {
            VRCPlayerApi p = _allPlayers[i];
            if (p == null || !p.IsValid()) continue;
            if (IsMuted(p.playerId)) RemoveMuted(p.playerId);
        }
        _voiceMode = 0;
        LogAction(local.displayName + " reset voice to default");
        // Voice settings applied by each client in NetApplyMutedState
        RequestSerialization();
        TriggerFlash();
        ApplyState(false);
    }

    // ==================== Cross-Game Reset ====================

    public void ResetCrack() { SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "NetResetCrack"); }
    public void ResetTrivia() { SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "NetResetTrivia"); }

    public void NetResetCrack()
    {
        if (!Networking.IsOwner(gameObject) || !CanModerate()) return;
        if (_crackTheCodeTarget != null)
        {
            _crackTheCodeTarget.SendCustomEvent("AdminReset");
            LogAction(Networking.LocalPlayer.displayName + " reset Crack the Code");
            RequestSerialization();
            TriggerFlash();
            ApplyState(false);
        }
    }

    public void NetResetTrivia()
    {
        if (!Networking.IsOwner(gameObject) || !CanModerate()) return;
        if (_triviaTarget != null)
        {
            _triviaTarget.SendCustomEvent("NetResetGame");
            LogAction(Networking.LocalPlayer.displayName + " reset Trivia");
            RequestSerialization();
            TriggerFlash();
            ApplyState(false);
        }
    }

    // ==================== Toggle Mute ====================

    public void ToggleMute() { SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "NetToggleMute"); }

    public void NetToggleMute()
    {
        if (!Networking.IsOwner(gameObject) || !CanModerate()) return;
        if (_selectedPlayer == null || !_selectedPlayer.IsValid()) return;
        VRCPlayerApi local = Networking.LocalPlayer;

        if (IsMuted(_selectedPlayer.playerId))
        {
            RemoveMuted(_selectedPlayer.playerId);
            LogAction(local.displayName + " unmuted " + _selectedPlayer.displayName);
        }
        else
        {
            AddMuted(_selectedPlayer.playerId);
            LogAction(local.displayName + " muted " + _selectedPlayer.displayName);
        }
        // Voice settings applied by each client in NetApplyMutedState (local player only)
        RequestSerialization();
        TriggerFlash();
        ApplyState(false);
    }

    /// <summary>Called from OnDeserialization — applies mute/voice state to the local player.</summary>
    private void NetApplyMutedState()
    {
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null) return;
        if (IsMuted(local.playerId))
        {
            local.SetVoiceGain(0f);
            local.SetVoiceDistanceFar(0f);
            local.SetVoiceDistanceNear(0f);
        }
        else if (_voiceMode == 2)
        {
            // Worldwide
            local.SetVoiceGain(25f);
            local.SetVoiceDistanceFar(1000000f);
            local.SetVoiceDistanceNear(0f);
        }
        else
        {
            // Normal
            local.SetVoiceGain(15f);
            local.SetVoiceDistanceFar(25f);
            local.SetVoiceDistanceNear(0f);
        }
    }

    // ==================== Ban / Unban ====================

    public void BanPlayer() { SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "NetBanPlayer"); }
    public void UnbanPlayer() { SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "NetUnbanPlayer"); }

    public void NetBanPlayer()
    {
        if (!Networking.IsOwner(gameObject) || !CanModerate()) return;
        if (_selectedPlayer == null || !_selectedPlayer.IsValid()) return;
        if (_selectedPlayer.isMaster) return;
        if (IsBanned(_selectedPlayer.playerId)) return;

        AddBanned(_selectedPlayer.playerId);
        AddFrozen(_selectedPlayer.playerId);
        LogAction(Networking.LocalPlayer.displayName + " banned " + _selectedPlayer.displayName);
        // Immobilize/Pickups/Jail applied by each client in NetApplyBannedState/NetApplyFrozenState
        RequestSerialization();
        TriggerFlash();
        ApplyState(false);
    }

    public void NetUnbanPlayer()
    {
        if (!Networking.IsOwner(gameObject) || !CanModerate()) return;
        if (_selectedPlayer == null || !_selectedPlayer.IsValid()) return;
        if (!IsBanned(_selectedPlayer.playerId)) return;

        RemoveBanned(_selectedPlayer.playerId);
        RemoveFrozen(_selectedPlayer.playerId);
        LogAction(Networking.LocalPlayer.displayName + " unbanned " + _selectedPlayer.displayName);
        // Unfreeze/Pickups applied by each client in NetApplyBannedState/NetApplyFrozenState
        RequestSerialization();
        TriggerFlash();
        ApplyState(false);
    }

    // ==================== Promote / Demote ====================

    public void PromotePlayer() { SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "NetPromotePlayer"); }
    public void DemotePlayer() { SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, "NetDemotePlayer"); }

    public void NetPromotePlayer()
    {
        if (!Networking.IsOwner(gameObject) || !_isAdmin) return;
        if (_selectedPlayer == null || !_selectedPlayer.IsValid()) return;
        if (_selectedPlayer.isMaster) return;
        if (IsModerator(_selectedPlayer.playerId)) return;

        AddModerator(_selectedPlayer.playerId);
        LogAction(Networking.LocalPlayer.displayName + " promoted " + _selectedPlayer.displayName + " to mod");
        RequestSerialization();
        TriggerFlash();
        ApplyState(false);
    }

    public void NetDemotePlayer()
    {
        if (!Networking.IsOwner(gameObject) || !_isAdmin) return;
        if (_selectedPlayer == null || !_selectedPlayer.IsValid()) return;
        if (!IsModerator(_selectedPlayer.playerId)) return;

        RemoveModerator(_selectedPlayer.playerId);
        LogAction(Networking.LocalPlayer.displayName + " demoted " + _selectedPlayer.displayName);
        RequestSerialization();
        TriggerFlash();
        ApplyState(false);
    }

    // ==================== Network Synced Actions ====================

    public void NetApplyFrozenState()
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        // Unfreeze players who were frozen locally but are no longer in the synced list
        for (int i = _locallyFrozenCount - 1; i >= 0; i--)
        {
            bool stillFrozen = false;
            for (int j = 0; j < _frozenCount; j++)
            {
                if (_locallyFrozenIds[i] == _frozenIds[j]) { stillFrozen = true; break; }
            }
            if (!stillFrozen)
            {
                if (lp != null && lp.playerId == _locallyFrozenIds[i])
                {
                    lp.Immobilize(false);
                    lp.SetRunSpeed(4f);
                    lp.SetWalkSpeed(2f);
                    lp.SetJumpImpulse(3f);
                }
                _locallyFrozenIds[i] = _locallyFrozenIds[_locallyFrozenCount - 1];
                _locallyFrozenCount--;
            }
        }

        // Freeze players in the synced frozen list (local player only)
        for (int i = 0; i < _frozenCount; i++)
        {
            bool alreadyTracked = false;
            for (int j = 0; j < _locallyFrozenCount; j++)
            {
                if (_locallyFrozenIds[j] == _frozenIds[i]) { alreadyTracked = true; break; }
            }
            if (!alreadyTracked && _locallyFrozenCount < 40)
            {
                _locallyFrozenIds[_locallyFrozenCount] = _frozenIds[i];
                _locallyFrozenCount++;
            }
            if (lp != null && lp.playerId == _frozenIds[i])
            {
                lp.Immobilize(true);
                lp.SetRunSpeed(0f);
                lp.SetWalkSpeed(0f);
                lp.SetJumpImpulse(0f);
            }
        }
    }

    public void NetApplyBannedState()
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        // Unban players who were banned locally but are no longer in the synced list
        for (int i = _locallyBannedCount - 1; i >= 0; i--)
        {
            bool stillBanned = false;
            for (int j = 0; j < _bannedCount; j++)
            {
                if (_locallyBannedIds[i] == _bannedIds[j]) { stillBanned = true; break; }
            }
            if (!stillBanned)
            {
                if (lp != null && lp.playerId == _locallyBannedIds[i])
                {
                    lp.Immobilize(false);
                    lp.EnablePickups(true);
                }
                _locallyBannedIds[i] = _locallyBannedIds[_locallyBannedCount - 1];
                _locallyBannedCount--;
            }
        }

        // Apply banned state to players in the synced banned list (local player only)
        for (int i = 0; i < _bannedCount; i++)
        {
            bool alreadyTracked = false;
            for (int j = 0; j < _locallyBannedCount; j++)
            {
                if (_locallyBannedIds[j] == _bannedIds[i]) { alreadyTracked = true; break; }
            }
            if (!alreadyTracked && _locallyBannedCount < 40)
            {
                _locallyBannedIds[_locallyBannedCount] = _bannedIds[i];
                _locallyBannedCount++;
            }
            if (lp != null && lp.playerId == _bannedIds[i])
            {
                if (_jailZone != null)
                {
                    lp.TeleportTo(_jailZone.position, _jailZone.rotation);
                }
                lp.Immobilize(true);
                lp.EnablePickups(false);
            }
        }
    }

    // ==================== Helpers ====================

    private bool CanModerate()
    {
        return _isAdmin || _isModerator;
    }

    /// <summary>
    /// Records an action to the synced mod log and the _lastAction field.
    /// Format: "{actor} {action} {target}" e.g. "Junyuh muted PlayerName".
    /// Call before RequestSerialization so the log syncs with the rest of the state.
    /// </summary>
    private void LogAction(string action)
    {
        _lastAction = action;
        // Shift log entries down (newest at index 0)
        for (int i = 7; i > 0; i--)
        {
            _modLog[i] = _modLog[i - 1];
        }
        _modLog[0] = action;
        if (_modLogCount < 8) _modLogCount++;
    }
}
