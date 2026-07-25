//using UnityEngine;
//using UnityEditor;

//public class PlayerDataTools : EditorWindow
//{
//    private static int selectedLevel = 1;
    
//    [MenuItem("Tools/Player Data Manager")]
//    public static void ShowWindow()
//    {
//        PlayerDataTools window = GetWindow<PlayerDataTools>("Player Data Manager");
//        window.minSize = new Vector2(300, 200);
//        window.Show();
//    }
    
//    private void OnGUI()
//    {
//        GUILayout.Space(10);
//        GUILayout.Label("Player Data Manager", EditorStyles.boldLabel);
//        GUILayout.Space(10);
        
//        // Current Level Display
//        int currentLevel = PlayerPrefs.GetInt(Const.player_level_key, 1);
//        EditorGUILayout.HelpBox($"Current Level: {currentLevel}", MessageType.Info);
        
//        GUILayout.Space(10);
        
//        // Set Specific Level
//        GUILayout.Label("Set Level", EditorStyles.boldLabel);
//        selectedLevel = EditorGUILayout.IntField("Level Number:", selectedLevel);
        
//        if (selectedLevel < 1) selectedLevel = 1;
        
//        if (GUILayout.Button("Set to Level " + selectedLevel, GUILayout.Height(30)))
//        {
//            SetLevel(selectedLevel);
//        }
        
//        GUILayout.Space(10);
        
//        // Quick Actions
//        GUILayout.Label("Quick Actions", EditorStyles.boldLabel);
        
//        EditorGUILayout.BeginHorizontal();
//        if (GUILayout.Button("Reset to Level 1"))
//        {
//            SetLevel(1);
//        }
//        if (GUILayout.Button("Level +1"))
//        {
//            SetLevel(currentLevel + 1);
//        }
//        if (GUILayout.Button("Level -1"))
//        {
//            if (currentLevel > 1)
//                SetLevel(currentLevel - 1);
//        }
//        EditorGUILayout.EndHorizontal();
        
//        GUILayout.Space(10);
        
//        // Star Count
//        int starCount = PlayerPrefs.GetInt("StarCount", 0);
//        EditorGUILayout.HelpBox($"Total Stars: {starCount}", MessageType.Info);
        
//        GUILayout.Space(10);
        
//        // Danger Zone
//        GUILayout.Label("Danger Zone", EditorStyles.boldLabel);
        
//        GUI.backgroundColor = Color.red;
//        if (GUILayout.Button("Clear All PlayerPrefs", GUILayout.Height(30)))
//        {
//            ClearAllPlayerPrefs();
//        }
//        GUI.backgroundColor = Color.white;
        
//        GUILayout.Space(10);
        
//        if (GUILayout.Button("Refresh", GUILayout.Height(25)))
//        {
//            Repaint();
//        }
//    }
    
//    private static void SetLevel(int level)
//    {
//        PlayerPrefs.SetInt(Const.player_level_key, level);
//        PlayerPrefs.Save();
        
//        ;
        
//        EditorUtility.DisplayDialog(
//            "Level Changed", 
//            $"Player level has been set to {level}!", 
//            "OK"
//        );
        
//        // Refresh window if open
//        if (HasOpenInstances<PlayerDataTools>())
//        {
//            GetWindow<PlayerDataTools>().Repaint();
//        }
//    }
    
//    [MenuItem("Tools/Reset Level to 1")]
//    public static void ResetLevelToOne()
//    {
//        SetLevel(1);
//    }
    
//    [MenuItem("Tools/Clear All PlayerPrefs")]
//    public static void ClearAllPlayerPrefs()
//    {
//        if (EditorUtility.DisplayDialog(
//            "Clear All Data", 
//            "Are you sure you want to delete ALL player data? This cannot be undone!", 
//            "Yes, Delete All", 
//            "Cancel"))
//        {
//            PlayerPrefs.DeleteAll();
//            PlayerPrefs.Save();
            
//            ;
            
//            EditorUtility.DisplayDialog(
//                "Data Cleared", 
//                "All player data has been deleted!", 
//                "OK"
//            );
            
//            // Refresh window if open
//            if (HasOpenInstances<PlayerDataTools>())
//            {
//                GetWindow<PlayerDataTools>().Repaint();
//            }
//        }
//    }
    
//    [MenuItem("Tools/Show Current Level")]
//    public static void ShowCurrentLevel()
//    {
//        int currentLevel = PlayerPrefs.GetInt(Const.player_level_key, 1);
        
//        ;
        
//        EditorUtility.DisplayDialog(
//            "Current Level", 
//            $"Current player level: {currentLevel}", 
//            "OK"
//        );
//    }
//}

