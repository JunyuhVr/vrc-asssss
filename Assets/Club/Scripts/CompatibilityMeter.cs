using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class CompatibilityMeter : UdonSharpBehaviour
{
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI player1Text;
    public TextMeshProUGUI player2Text;
    public TextMeshProUGUI resultText;
    public Image meterFill;
    public Image loversButtonImage;
    public Image friendsButtonImage;
    public Image shotButtonImage;
    public GameObject player1JoinButton;
    public GameObject player2JoinButton;
    public GameObject calculateButton;
    public float resultHoldSeconds = 12f;

    [UdonSynced] private int syncedMode;
    [UdonSynced] private int syncedPlayer1Id = -1;
    [UdonSynced] private string syncedPlayer1Name = "";
    [UdonSynced] private int syncedPlayer2Id = -1;
    [UdonSynced] private string syncedPlayer2Name = "";
    [UdonSynced] private int syncedScore = -1;
    [UdonSynced] private int syncedPhase;
    [UdonSynced] private int syncedRevision;

    private float displayedScore;
    private bool animating;
    private int scheduledResetRevision = -1;

    private readonly Color selectedColor = new Color(0f, 0.72f, 1f, 1f);
    private readonly Color unselectedColor = new Color(0.025f, 0.18f, 0.28f, 1f);

    private void Start()
    {
        AutoFindUi();
        ApplyState(false);
    }

    private void AutoFindUi()
    {
        Transform panel = transform.Find("GlassPanel");
        if (panel == null) return;
        if (titleText == null) titleText = FindTMP(panel, "Title");
        if (player1Text == null) player1Text = FindTMP(panel, "PlayerOneCard/PlayerLabel");
        if (player2Text == null) player2Text = FindTMP(panel, "PlayerTwoCard/PlayerLabel");
        if (resultText == null) resultText = FindTMP(panel, "ResultText");
        if (meterFill == null) meterFill = FindImage(panel, "MeterTrack/MeterGlow");
        if (loversButtonImage == null) loversButtonImage = FindImage(panel, "LoversButton");
        if (friendsButtonImage == null) friendsButtonImage = FindImage(panel, "FriendsButton");
        if (shotButtonImage == null) shotButtonImage = FindImage(panel, "ShotButton");
        if (player1JoinButton == null) player1JoinButton = FindGO(panel, "PlayerOneCard/JoinButton");
        if (player2JoinButton == null) player2JoinButton = FindGO(panel, "PlayerTwoCard/JoinButton");
        if (calculateButton == null) calculateButton = FindGO(panel, "CalculateButton");
        if (meterFill != null)
        {
            meterFill.type = Image.Type.Filled;
            meterFill.fillMethod = Image.FillMethod.Horizontal;
            meterFill.fillAmount = 0f;
        }
    }

    private TextMeshProUGUI FindTMP(Transform root, string path)
    {
        Transform t = root.Find(path);
        return t == null ? null : t.GetComponent<TextMeshProUGUI>();
    }

    private Image FindImage(Transform root, string path)
    {
        Transform t = root.Find(path);
        return t == null ? null : t.GetComponent<Image>();
    }

    private GameObject FindGO(Transform root, string path)
    {
        Transform t = root.Find(path);
        return t == null ? null : t.gameObject;
    }

    private void Update()
    {
        if (!animating || meterFill == null) return;

        float target = syncedScore * 0.01f;
        displayedScore = Mathf.MoveTowards(displayedScore, target, Time.deltaTime * 0.45f);
        meterFill.fillAmount = displayedScore;
        resultText.text = Mathf.RoundToInt(displayedScore * 100f) + "%";

        if (displayedScore >= target)
        {
            animating = false;
            ShowResult();
        }
    }

    public void JoinPlayerOne()
    {
        NetworkCalling.SendCustomNetworkEvent((IUdonEventReceiver)this, NetworkEventTarget.Owner, nameof(RequestJoinPlayer), 0);
    }

    public void JoinPlayerTwo()
    {
        NetworkCalling.SendCustomNetworkEvent((IUdonEventReceiver)this, NetworkEventTarget.Owner, nameof(RequestJoinPlayer), 1);
    }

    public void SelectLovers()
    {
        NetworkCalling.SendCustomNetworkEvent((IUdonEventReceiver)this, NetworkEventTarget.Owner, nameof(RequestSetMode), 0);
    }

    public void SelectFriends()
    {
        NetworkCalling.SendCustomNetworkEvent((IUdonEventReceiver)this, NetworkEventTarget.Owner, nameof(RequestSetMode), 1);
    }

    public void SelectShotPicker()
    {
        NetworkCalling.SendCustomNetworkEvent((IUdonEventReceiver)this, NetworkEventTarget.Owner, nameof(RequestSetMode), 2);
    }

    public void Calculate()
    {
        NetworkCalling.SendCustomNetworkEvent((IUdonEventReceiver)this, NetworkEventTarget.Owner, nameof(RequestCalculate));
    }

    [NetworkCallable]
    public void RequestJoinPlayer(int slot)
    {
        if (!Networking.IsOwner(gameObject)) return;
        VRCPlayerApi caller = NetworkCalling.CallingPlayer;
        if (caller == null || !caller.IsValid()) return;
        TryJoinPlayer(caller, slot == 0);
    }

    [NetworkCallable]
    public void RequestSetMode(int mode)
    {
        if (!Networking.IsOwner(gameObject)) return;
        OwnerSetMode(mode);
    }

    [NetworkCallable]
    public void RequestCalculate()
    {
        if (!Networking.IsOwner(gameObject)) return;
        CalculateAsOwner();
    }

    public void OwnerResetAfterDelay()
    {
        if (!Networking.IsOwner(gameObject) || syncedPhase != 2 || scheduledResetRevision != syncedRevision) return;
        ResetRound();
    }

    public override void OnDeserialization()
    {
        ApplyState(syncedPhase == 2);
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        if (player != null && player.isLocal && syncedPhase == 2)
        {
            scheduledResetRevision = syncedRevision;
            SendCustomEventDelayedSeconds(nameof(OwnerResetAfterDelay), 5f);
        }
    }

    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        if (!Networking.IsOwner(gameObject) || !Utilities.IsValid(player)) return;
        bool changed = false;
        if (player.playerId == syncedPlayer1Id)
        {
            syncedPlayer1Id = -1;
            syncedPlayer1Name = "";
            changed = true;
        }
        if (player.playerId == syncedPlayer2Id)
        {
            syncedPlayer2Id = -1;
            syncedPlayer2Name = "";
            changed = true;
        }
        if (changed) CommitWaitingState();
    }

    private void TryJoinPlayer(VRCPlayerApi player, bool firstSlot)
    {
        if (!Utilities.IsValid(player) || syncedPhase == 2) return;
        // TESTING: allow same player to join both slots
        // if (player.playerId == syncedPlayer1Id || player.playerId == syncedPlayer2Id) return;

        if (firstSlot)
        {
            if (syncedPlayer1Id != -1) return;
            syncedPlayer1Id = player.playerId;
            syncedPlayer1Name = player.displayName;
        }
        else
        {
            if (syncedPlayer2Id != -1) return;
            syncedPlayer2Id = player.playerId;
            syncedPlayer2Name = player.displayName;
        }
        CommitWaitingState();
    }

    private void CalculateAsOwner()
    {
        if (syncedPhase != 1) return;
        if (!Utilities.IsValid(VRCPlayerApi.GetPlayerById(syncedPlayer1Id)) ||
            !Utilities.IsValid(VRCPlayerApi.GetPlayerById(syncedPlayer2Id)))
        {
            RemoveInvalidPlayers();
            CommitWaitingState();
            return;
        }

        syncedScore = Random.Range(10, 101);
        syncedPhase = 2;
        syncedRevision++;
        RequestSerialization();
        ApplyState(true);
        scheduledResetRevision = syncedRevision;
        SendCustomEventDelayedSeconds(nameof(OwnerResetAfterDelay), resultHoldSeconds);
    }

    private void OwnerSetMode(int mode)
    {
        if (!Networking.IsOwner(gameObject) || syncedPhase == 2) return;
        syncedMode = mode;
        syncedScore = -1;
        syncedRevision++;
        RequestSerialization();
        ApplyState(false);
    }

    private void CommitWaitingState()
    {
        syncedScore = -1;
        syncedPhase = syncedPlayer1Id != -1 && syncedPlayer2Id != -1 ? 1 : 0;
        syncedRevision++;
        RequestSerialization();
        ApplyState(false);
    }

    private void RemoveInvalidPlayers()
    {
        if (!Utilities.IsValid(VRCPlayerApi.GetPlayerById(syncedPlayer1Id)))
        {
            syncedPlayer1Id = -1;
            syncedPlayer1Name = "";
        }
        if (!Utilities.IsValid(VRCPlayerApi.GetPlayerById(syncedPlayer2Id)))
        {
            syncedPlayer2Id = -1;
            syncedPlayer2Name = "";
        }
    }

    private void ResetRound()
    {
        syncedPlayer1Id = -1;
        syncedPlayer1Name = "";
        syncedPlayer2Id = -1;
        syncedPlayer2Name = "";
        syncedScore = -1;
        syncedPhase = 0;
        syncedRevision++;
        RequestSerialization();
        ApplyState(false);
    }

    private void ApplyState(bool animateResult)
    {
        if (titleText != null) titleText.text = GetModeTitle();
        if (player1Text != null) player1Text.text = syncedPlayer1Id == -1 ? "PLAYER ONE" : syncedPlayer1Name;
        if (player2Text != null) player2Text.text = syncedPlayer2Id == -1 ? "PLAYER TWO" : syncedPlayer2Name;

        if (loversButtonImage != null) loversButtonImage.color = syncedMode == 0 ? selectedColor : unselectedColor;
        if (friendsButtonImage != null) friendsButtonImage.color = syncedMode == 1 ? selectedColor : unselectedColor;
        if (shotButtonImage != null) shotButtonImage.color = syncedMode == 2 ? selectedColor : unselectedColor;

        if (player1JoinButton != null) player1JoinButton.SetActive(syncedPlayer1Id == -1 && syncedPhase != 2);
        if (player2JoinButton != null) player2JoinButton.SetActive(syncedPlayer2Id == -1 && syncedPhase != 2);
        if (calculateButton != null) calculateButton.SetActive(syncedPhase == 1);

        if (meterFill != null && syncedPhase != 2)
        {
            meterFill.fillAmount = 0f;
            displayedScore = 0f;
        }

        if (syncedPhase == 0)
        {
            animating = false;
            if (resultText != null) resultText.text = "WAITING FOR TWO PLAYERS";
        }
        else if (syncedPhase == 1)
        {
            animating = false;
            if (resultText != null) resultText.text = "BOTH PLAYERS READY";
        }
        else if (animateResult)
        {
            displayedScore = 0f;
            animating = true;
            if (meterFill != null) meterFill.fillAmount = 0f;
            if (resultText != null) resultText.text = "CALCULATING...";
        }
        else
        {
            displayedScore = syncedScore * 0.01f;
            if (meterFill != null) meterFill.fillAmount = displayedScore;
            ShowResult();
        }
    }

    private void ShowResult()
    {
        if (resultText == null) return;
        string names = syncedPlayer1Name + " + " + syncedPlayer2Name;
        if (syncedMode == 0)
        {
            if (syncedScore >= 85) resultText.text = names + "  •  " + syncedScore + "%  SOULMATES";
            else if (syncedScore >= 60) resultText.text = names + "  •  " + syncedScore + "%  STRONG SPARK";
            else resultText.text = names + "  •  " + syncedScore + "%  KEEP TALKING";
        }
        else if (syncedMode == 1)
        {
            if (syncedScore >= 85) resultText.text = names + "  •  " + syncedScore + "%  BEST FRIEND ENERGY";
            else if (syncedScore >= 60) resultText.text = names + "  •  " + syncedScore + "%  GREAT VIBES";
            else resultText.text = names + "  •  " + syncedScore + "%  NEW FRIENDS";
        }
        else
        {
            resultText.text = syncedScore >= 55
                ? syncedPlayer1Name + " TAKES THE SHOT"
                : syncedPlayer2Name + " TAKES THE SHOT";
        }
    }

    private string GetModeTitle()
    {
        if (syncedMode == 0) return "LOVERS COMPATIBILITY";
        if (syncedMode == 1) return "FRIENDS COMPATIBILITY";
        return "SHOT PICKER";
    }
}
