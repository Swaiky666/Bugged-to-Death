#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

[CustomEditor(typeof(RoomSystem))]
public class RoomSystemEditor : Editor
{
    private bool showCurrentRoomBugs = true;
    private bool showPlayerAssignmentSettings = true;
    private Vector2 bugListScrollPosition = Vector2.zero;

    public override void OnInspectorGUI()
    {
        RoomSystem roomSystem = (RoomSystem)target;

        // 绘制默认Inspector
        DrawDefaultInspector();

        EditorGUILayout.Space();

        // 玩家分配状态区域
        if (Application.isPlaying)
        {
            DrawPlayerAssignmentStatus(roomSystem);
            EditorGUILayout.Space();
        }

        // 当前房间Bug信息区域
        if (Application.isPlaying && roomSystem != null)
        {
            DrawCurrentRoomBugInfo(roomSystem);
            EditorGUILayout.Space();
        }

        EditorGUILayout.LabelField("房间系统控制", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        // 重新初始化按钮
        if (GUILayout.Button("重新初始化房间"))
        {
            roomSystem.EditorInitializeRooms();
        }

        // 检测玩家位置按钮
        if (GUILayout.Button("强制检测位置"))
        {
            roomSystem.ForceCheckPlayerPosition();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        // 查找玩家对象按钮
        if (GUILayout.Button("查找玩家对象"))
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                Selection.activeGameObject = player;
                Debug.Log($"找到玩家: {player.name}, 位置: {player.transform.position}");

                // 如果找到玩家，可以选择自动分配给房间系统
                if (Application.isPlaying && !roomSystem.HasValidPlayer())
                {
                    if (EditorUtility.DisplayDialog("发现玩家",
                        $"找到玩家对象: {player.name}\n是否将其分配给房间系统？",
                        "是", "否"))
                    {
                        roomSystem.SetPlayer(player.transform);
                    }
                }
            }
            else
            {
                Debug.LogWarning("场景中没有找到Tag为'Player'的对象");
            }
        }

        // 验证房间位置按钮
        if (GUILayout.Button("验证房间位置"))
        {
            roomSystem.VerifyRoomPositions();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        // 显示距离信息按钮
        if (GUILayout.Button("显示距离信息"))
        {
            roomSystem.ShowDistanceInfo();
        }

        // 显示当前布局按钮
        if (GUILayout.Button("显示当前布局"))
        {
            roomSystem.LogCurrentLayout();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("玩家分配控制", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("显示分配状态"))
        {
            roomSystem.ShowPlayerAssignmentStatus();
        }

        if (GUILayout.Button("强制等待玩家"))
        {
            roomSystem.ForceWaitForPlayer();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("清除玩家引用"))
        {
            if (EditorUtility.DisplayDialog("清除玩家引用",
                "确定要清除当前的玩家引用吗？这可能会影响房间系统的正常运行。",
                "确定", "取消"))
            {
                roomSystem.ClearPlayer();
            }
        }

        // 手动分配玩家按钮
        if (GUILayout.Button("手动分配玩家"))
        {
            GameObject selectedPlayer = Selection.activeGameObject;
            if (selectedPlayer != null && selectedPlayer.CompareTag("Player"))
            {
                roomSystem.SetPlayer(selectedPlayer.transform);
                Debug.Log($"手动分配玩家: {selectedPlayer.name}");
            }
            else
            {
                EditorUtility.DisplayDialog("无效选择",
                    "请先在Hierarchy中选择一个带有'Player'标签的GameObject，然后点击此按钮。",
                    "确定");
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Bug追踪控制", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("重新扫描所有Bug"))
        {
            roomSystem.RescanAllBugs();
        }

        if (GUILayout.Button("显示详细Bug信息"))
        {
            roomSystem.ShowDetailedBugInfo();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("显示当前房间Bug"))
        {
            roomSystem.ShowCurrentRoomBugInfo();
        }

        if (GUILayout.Button("刷新当前房间信息"))
        {
            roomSystem.RefreshCurrentRoomBugInfo();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("显示激活Bug"))
        {
            roomSystem.ShowCurrentRoomActiveBugs();
        }

        if (GUILayout.Button("测试游戏完成"))
        {
            roomSystem.TestGameComplete();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("清理销毁对象"))
        {
            roomSystem.CleanupAllDestroyedBugs();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("环形移动测试", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("测试环形移动"))
        {
            roomSystem.TestRingMovement();
        }

        if (GUILayout.Button("显示边界房间"))
        {
            roomSystem.ShowBoundaryRooms();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("模拟向右移动"))
        {
            roomSystem.TestMoveRight();
        }

        if (GUILayout.Button("模拟向左移动"))
        {
            roomSystem.TestMoveLeft();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // 运行时信息显示
        if (Application.isPlaying)
        {
            DrawRuntimeInfo(roomSystem);
        }

        // 帮助信息
        DrawHelpInfo();

        // 如果有修改，标记为脏数据
        if (GUI.changed)
        {
            EditorUtility.SetDirty(roomSystem);
        }
    }

    private void DrawPlayerAssignmentStatus(RoomSystem roomSystem)
    {
        EditorGUILayout.LabelField("玩家分配状态", EditorStyles.boldLabel);

        // 创建一个区域来显示玩家分配信息
        EditorGUILayout.BeginVertical(GUI.skin.box);

        // 获取玩家引用状态
        Transform currentPlayer = roomSystem.GetPlayer();
        bool hasValidPlayer = roomSystem.HasValidPlayer();
        bool isWaitingForPlayer = roomSystem.IsWaitingForPlayer();

        // 状态颜色指示
        Color originalColor = GUI.backgroundColor;
        if (hasValidPlayer)
            GUI.backgroundColor = Color.green;
        else if (isWaitingForPlayer)
            GUI.backgroundColor = Color.yellow;
        else
            GUI.backgroundColor = Color.red;

        // 状态信息
        string statusText = hasValidPlayer ? "玩家已分配" :
                           isWaitingForPlayer ? "等待玩家分配" : "未分配玩家";

        EditorGUILayout.LabelField($"分配状态: {statusText}");

        GUI.backgroundColor = originalColor;

        if (currentPlayer != null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"当前玩家: {currentPlayer.name}");
            if (GUILayout.Button("选中", GUILayout.Width(60)))
            {
                Selection.activeGameObject = currentPlayer.gameObject;
                EditorGUIUtility.PingObject(currentPlayer.gameObject);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField($"玩家位置: {currentPlayer.position.ToString("F2")}");
        }
        else
        {
            EditorGUILayout.LabelField("当前玩家: 无");
        }

        EditorGUILayout.LabelField($"等待玩家模式: {(isWaitingForPlayer ? "是" : "否")}");

        // 快速操作按钮
        EditorGUILayout.BeginHorizontal();

        if (!hasValidPlayer)
        {
            if (GUILayout.Button("自动查找玩家"))
            {
                GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
                if (players.Length > 0)
                {
                    if (players.Length == 1)
                    {
                        roomSystem.SetPlayer(players[0].transform);
                        Debug.Log($"自动分配玩家: {players[0].name}");
                    }
                    else
                    {
                        // 如果找到多个玩家，显示选择菜单
                        GenericMenu menu = new GenericMenu();
                        for (int i = 0; i < players.Length; i++)
                        {
                            GameObject player = players[i];
                            menu.AddItem(new GUIContent($"{player.name} (位置: {player.transform.position})"),
                                       false, () => {
                                           roomSystem.SetPlayer(player.transform);
                                           Debug.Log($"选择玩家: {player.name}");
                                       });
                        }
                        menu.ShowAsContext();
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog("未找到玩家",
                        "场景中没有找到带有'Player'标签的GameObject。",
                        "确定");
                }
            }
        }

        if (hasValidPlayer && GUILayout.Button("重新分配"))
        {
            roomSystem.ClearPlayer();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawCurrentRoomBugInfo(RoomSystem roomSystem)
    {
        EditorGUILayout.LabelField("当前房间Bug信息", EditorStyles.boldLabel);

        // 创建一个区域来显示当前房间Bug信息
        EditorGUILayout.BeginVertical(GUI.skin.box);

        // 基本信息
        EditorGUILayout.LabelField($"当前房间序列: {roomSystem.GetCurrentRoomSequence()}");
        EditorGUILayout.LabelField($"Bug统计: {roomSystem.GetCurrentRoomBugStats()}");

        // 获取有效的Bug对象（已过滤null对象）
        var currentRoomBugs = roomSystem.GetCurrentRoomBugObjects();
        var activeBugs = roomSystem.GetCurrentRoomActiveBugs();
        var inactiveBugs = roomSystem.GetCurrentRoomInactiveBugs();

        EditorGUILayout.LabelField($"总Bug数: {currentRoomBugs.Count} | 激活: {activeBugs.Count} | 未激活: {inactiveBugs.Count}");

        // 折叠控制
        showCurrentRoomBugs = EditorGUILayout.Foldout(showCurrentRoomBugs, $"Bug对象列表 ({currentRoomBugs.Count}个)", true);

        if (showCurrentRoomBugs && currentRoomBugs.Count > 0)
        {
            // 滚动区域
            bugListScrollPosition = EditorGUILayout.BeginScrollView(bugListScrollPosition, GUILayout.MaxHeight(150));

            for (int i = 0; i < currentRoomBugs.Count; i++)
            {
                var bug = currentRoomBugs[i];
                // 由于GetCurrentRoomBugObjects()已经过滤了null对象，这里可以安全访问
                if (bug != null)
                {
                    EditorGUILayout.BeginHorizontal();

                    // Bug状态颜色指示
                    Color originalColor = GUI.backgroundColor;
                    if (bug.IsBugActive())
                        GUI.backgroundColor = Color.red;
                    else if (bug.IsBeingFixed())
                        GUI.backgroundColor = Color.yellow;
                    else
                        GUI.backgroundColor = Color.green;

                    // Bug对象字段（可点击选中）
                    if (GUILayout.Button($"{i + 1}. {bug.name}", EditorStyles.objectField, GUILayout.ExpandWidth(true)))
                    {
                        Selection.activeGameObject = bug.gameObject;
                        EditorGUIUtility.PingObject(bug.gameObject);
                    }

                    GUI.backgroundColor = originalColor;

                    // Bug类型和状态
                    string status = bug.IsBugActive() ? "激活" : "未激活";
                    if (bug.IsBeingFixed()) status = "修复中";

                    EditorGUILayout.LabelField($"({bug.GetBugType()}) - {status}", GUILayout.Width(150));

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndScrollView();

            // 快速操作按钮
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("选中所有激活Bug"))
            {
                var activeObjects = new List<Object>();
                foreach (var bug in activeBugs)
                {
                    if (bug != null) activeObjects.Add(bug.gameObject);
                }
                Selection.objects = activeObjects.ToArray();
            }

            if (GUILayout.Button("选中所有未激活Bug"))
            {
                var inactiveObjects = new List<Object>();
                foreach (var bug in inactiveBugs)
                {
                    if (bug != null) inactiveObjects.Add(bug.gameObject);
                }
                Selection.objects = inactiveObjects.ToArray();
            }
            EditorGUILayout.EndHorizontal();
        }
        else if (currentRoomBugs.Count == 0)
        {
            EditorGUILayout.LabelField("当前房间没有有效的Bug对象", EditorStyles.helpBox);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawRuntimeInfo(RoomSystem roomSystem)
    {
        EditorGUILayout.LabelField("运行时状态", EditorStyles.boldLabel);

        // 使用反射获取私有字段信息
        var currentSeqField = typeof(RoomSystem).GetField("currentRoomSequence",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var roomInstancesField = typeof(RoomSystem).GetField("roomInstances",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var lastDetectionField = typeof(RoomSystem).GetField("lastDetectionTime",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var isInitializedField = typeof(RoomSystem).GetField("isInitialized",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (currentSeqField != null && roomInstancesField != null)
        {
            int currentSeq = (int)currentSeqField.GetValue(roomSystem);
            var roomInstances = roomInstancesField.GetValue(roomSystem) as System.Collections.IList;
            float lastDetection = lastDetectionField != null ? (float)lastDetectionField.GetValue(roomSystem) : 0f;
            bool isInitialized = isInitializedField != null ? (bool)isInitializedField.GetValue(roomSystem) : false;

            EditorGUILayout.LabelField($"初始化状态: {(isInitialized ? "已初始化" : "未初始化")}");
            EditorGUILayout.LabelField($"当前玩家房间序列: {currentSeq}");
            EditorGUILayout.LabelField($"活动房间实例数: {roomInstances?.Count ?? 0}");
            EditorGUILayout.LabelField($"上次检测时间: {lastDetection:F2}s");
            EditorGUILayout.LabelField($"检测间隔: {roomSystem.detectionInterval:F2}s");

            // 玩家分配状态
            EditorGUILayout.LabelField($"玩家分配状态: {(roomSystem.HasValidPlayer() ? "已分配" : "未分配")}");
            EditorGUILayout.LabelField($"等待玩家中: {(roomSystem.IsWaitingForPlayer() ? "是" : "否")}");

            // 全局Bug统计
            EditorGUILayout.LabelField($"全局Bug统计: {roomSystem.GetGlobalBugStats()}");
            EditorGUILayout.LabelField($"剩余Bug数量: {roomSystem.GetRemainingBugCount()}");
            EditorGUILayout.LabelField($"当前房间未修复Bug: {(roomSystem.CurrentRoomHasUnfixedBugs() ? "有" : "无")}");
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("实时距离检测"))
        {
            roomSystem.ShowDistanceInfo();
        }
        if (GUILayout.Button("输出详细状态"))
        {
            Debug.Log("=== 房间系统详细状态 ===");
            roomSystem.LogCurrentLayout();
            roomSystem.VerifyRoomPositions();
            roomSystem.ShowDistanceInfo();
            roomSystem.ShowCurrentRoomBugInfo();
            roomSystem.ShowPlayerAssignmentStatus();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("强制完成初始化"))
        {
            // 使用反射检查是否已初始化
            bool isInitialized = isInitializedField != null ? (bool)isInitializedField.GetValue(roomSystem) : false;
            if (!isInitialized && roomSystem.HasValidPlayer())
            {
                // 通过调用私有方法完成初始化
                var completeInitMethod = typeof(RoomSystem).GetMethod("CompleteInitialization",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (completeInitMethod != null)
                {
                    completeInitMethod.Invoke(roomSystem, null);
                    Debug.Log("强制完成房间系统初始化");
                }
            }
            else if (isInitialized)
            {
                EditorUtility.DisplayDialog("已初始化", "房间系统已经完成初始化。", "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("缺少玩家", "需要先分配玩家才能完成初始化。", "确定");
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawHelpInfo()
    {
        EditorGUILayout.HelpBox(
            "基于玩家主动分配的环形房间循环系统 + Bug追踪功能：\n\n" +
            "🎮 玩家分配机制：\n" +
            "• 玩家在实例化时主动向房间系统注册自己的引用\n" +
            "• Player脚本在Awake中查找'RoomSystem(Clone)'对象\n" +
            "• 调用roomSystem.SetPlayer(transform)传递引用\n" +
            "• 房间系统可以等待玩家分配，避免空引用错误\n" +
            "• 提供后备机制：如果等待超时，降级到Tag查找方式\n\n" +
            "🔄 环形循环机制：\n" +
            "• 完全移除Trigger，使用纯距离计算检测玩家位置\n" +
            "• 玩家向右移动时：将最左边的房间移动到最右边\n" +
            "• 玩家向左移动时：将最右边的房间移动到最左边\n" +
            "• 每次只移动一个房间，其他房间保持原位置\n" +
            "• 形成一个'环形队列'，房间在环上循环复用\n\n" +
            "🎯 Bug追踪系统：\n" +
            "• 自动扫描每个房间中的所有BugObject组件\n" +
            "• 实时追踪Bug修复状态和剩余数量\n" +
            "• 提供当前房间Bug信息的详细显示\n" +
            "• 支持按房间序列查询Bug信息\n" +
            "• 自动更新当前房间Bug列表（可开关）\n" +
            "• 自动清理已销毁的Bug对象引用\n\n" +
            "🔗 新增功能：\n" +
            "• Player主动注册到房间系统，避免查找依赖\n" +
            "• 房间系统等待机制，支持异步初始化\n" +
            "• Inspector实时显示玩家分配状态\n" +
            "• 手动分配玩家功能，便于调试\n" +
            "• 多玩家场景支持，可选择分配哪个玩家\n" +
            "• 强制初始化功能，便于开发调试\n\n" +
            "🎮 外部调用接口：\n" +
            "• SetPlayer(Transform) - 设置玩家引用\n" +
            "• GetPlayer() - 获取当前玩家引用\n" +
            "• HasValidPlayer() - 检查是否有有效玩家\n" +
            "• IsWaitingForPlayer() - 检查是否在等待玩家分配\n" +
            "• GetCurrentRoomBugObjects() - 获取当前房间所有有效Bug\n" +
            "• GetCurrentRoomActiveBugs() - 获取当前房间激活Bug\n" +
            "• CurrentRoomHasUnfixedBugs() - 检查是否有未修复Bug\n\n" +
            "🔍 调试工具：\n" +
            "• Inspector实时显示玩家分配和连接状态\n" +
            "• 实时显示当前房间有效Bug列表\n" +
            "• 点击Bug对象可直接选中并定位\n" +
            "• 颜色区分：红色=激活Bug，黄色=修复中，绿色=未激活\n" +
            "• Console输出详细的Bug状态和移动日志\n" +
            "• Scene视图实时显示序列号、距离和Bug统计\n" +
            "• 自动过滤已销毁的Bug对象，不再显示null引用\n" +
            "• 强制初始化和玩家分配控制，便于开发测试\n\n" +
            "⚙️ 推荐工作流程：\n" +
            "1. 设置房间预制体列表和参数\n" +
            "2. 确保玩家对象有正确的'Player'标签\n" +
            "3. 启用'等待玩家分配'模式\n" +
            "4. 运行游戏，玩家会自动注册到房间系统\n" +
            "5. 如果出现问题，使用Inspector的调试按钮排查\n" +
            "6. 使用'输出详细状态'按钮获取完整系统信息",
            MessageType.Info
        );
    }
}
#endif // UNITY_EDITOR