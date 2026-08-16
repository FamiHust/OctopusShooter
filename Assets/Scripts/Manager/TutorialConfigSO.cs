using System.Collections.Generic;
using UnityEngine;



[System.Serializable]
public class TutorialStep
{
    [Header("Step Info")]
    public string stepID;               // "step_01_click_play"
    public float nextStepDelay = 0;

    [Header("Target - Click Type")]
    public string targetObjectName;     // Tên GameObject cần focus (VD: "PlayButton") - Chỉ dùng cho Click type

    [Header("Instruction")]
    [TextArea(2, 4)]
    public string description;          // "Nhấn vào đây để bắt đầu chơi!"

    [Header("Arrow - Click Type")]
    public Vector3 arrowOffset;         // World offset của arrow so với target (VD: (0, 1, 0) = lên trên theo world)
    public float arrowRotation;         // Rotation của arrow (VD: 0 = point down, 180 = point up)


    [Header("Arrow Animation")]
    public float arrowLoopDelay = 1f; // Delay trước khi lặp lại animation (sau khi disappear)

    [Header("Behavior")]
    public bool requireClick;           // Phải click target mới next? (Click type only)
    public bool enableOverlay = true;

    [Header("Progress Gate")]
    public bool waitForMagicStoneProgress;
    [Min(1)] public int requiredMagicStoneCount = 3;

}

[CreateAssetMenu(fileName = "TutorialConfig", menuName = "Game/Tutorial Config")]
public class TutorialConfigSO : ScriptableObject
{

    [Tooltip("Unique name for this tutorial - will be saved in PlayerPrefs")]
    public string tutorialName; // VD: "Tutorial_Menu_PlayButton" hoặc "Tutorial_InGame_Hammer"

    [Tooltip("Level để trigger tutorial này")]
    public int tutorialLevel;

    [Header("Story / Narrative")]
    [Tooltip("Story Type cần phát trước khi vào level tutorial này (để None nếu không có)")]
    public StoryType storyType = StoryType.None;

    [Header("Tutorial Steps")]
    public List<TutorialStep> steps = new List<TutorialStep>();

    // Helper method
    public TutorialStep GetStep(int index)
    {
        if (index < 0 || index >= steps.Count) return null;
        return steps[index];
    }
}
