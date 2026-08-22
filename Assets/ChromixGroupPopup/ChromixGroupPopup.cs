using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.Economy;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ChromixGroupPopup : UdonSharpBehaviour
{
    [Tooltip("Group ID from vrchat.com group URL, e.g. grp_xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx")]
    public string groupId = "grp_07a66a18-762d-48c6-8e07-d2ef08388546";

    private RectTransform popupRect;
    private CanvasGroup popupGroup;
    private Button joinButton;
    private Button closeButton;
    private Text statusText;

    private bool shown;
    private bool animating;
    private float animTime;
    private float animDuration = 0.6f;
    private Vector2 hiddenPos;
    private Vector2 shownPos;

    private float autoShowDelay = 3f;
    private float autoShowTimer;

    private void Start()
    {
        popupRect = (RectTransform)transform;
        popupGroup = GetComponent<CanvasGroup>();
        popupGroup.alpha = 0f;
        popupGroup.interactable = false;
        popupGroup.blocksRaycasts = false;

        shownPos = popupRect.anchoredPosition;
        hiddenPos = shownPos + new Vector2(0f, -600f);
        popupRect.anchoredPosition = hiddenPos;

        Transform joinT = transform.Find("JoinGroupButton");
        if (joinT != null) joinButton = joinT.GetComponent<Button>();

        Transform closeT = transform.Find("CloseButton");
        if (closeT != null) closeButton = closeT.GetComponent<Button>();

        Transform statusT = transform.Find("StatusText");
        if (statusT != null) statusText = statusT.GetComponent<Text>();

        autoShowTimer = 0f;
        Debug.Log("[ChromixPopup] Start complete. groupId=" + groupId);
    }

    private void Update()
    {
        if (animating)
        {
            animTime += Time.deltaTime;
            float t = Mathf.Clamp01(animTime / animDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            if (shown)
            {
                popupRect.anchoredPosition = Vector2.Lerp(hiddenPos, shownPos, eased);
                popupGroup.alpha = eased;
                popupGroup.interactable = t >= 1f;
                popupGroup.blocksRaycasts = t >= 1f;
            }
            else
            {
                popupRect.anchoredPosition = Vector2.Lerp(shownPos, hiddenPos, eased);
                popupGroup.alpha = 1f - eased;
                popupGroup.interactable = false;
                popupGroup.blocksRaycasts = false;
            }

            if (t >= 1f) animating = false;
            return;
        }

        if (!shown)
        {
            autoShowTimer += Time.deltaTime;
            if (autoShowTimer >= autoShowDelay)
            {
                ShowPopup();
            }
        }
    }

    public void ShowPopup()
    {
        if (shown || animating) return;
        shown = true;
        animating = true;
        animTime = 0f;
        if (statusText != null) statusText.text = "JOIN THE CHROMIX GROUP";
        Debug.Log("[ChromixPopup] Showing popup");
    }

    public void HidePopup()
    {
        if (!shown || animating) return;
        shown = false;
        animating = true;
        animTime = 0f;
        Debug.Log("[ChromixPopup] Hiding popup");
    }

    public void JoinGroup()
    {
        Debug.Log("[ChromixPopup] JoinGroup clicked. groupId=" + groupId);
        if (string.IsNullOrEmpty(groupId))
        {
            Debug.LogError("[ChromixPopup] Group ID is not set!");
            return;
        }
        Store.OpenGroupPage(groupId);
        if (statusText != null) statusText.text = "OPENING GROUP PAGE...";
    }
}
