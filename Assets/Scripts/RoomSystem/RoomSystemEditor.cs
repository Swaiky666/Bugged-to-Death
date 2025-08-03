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
    private bool showDetectionSettings = true; // 新增：检测设置折叠状态
    private bool showDetectionStatus = false; // 新增：检测状态折叠状态
    private Vector2 bugListScrollPosition = Vector2.zero;
    private Vector2 detectionStatusScrollPosition = Vector2.zero; // 新增：检测状态滚动位置

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

        // 房间检测设置和状态区域
        if (Application.isPlaying && roomSystem != null)
        {
            DrawRoomDetectionSettings(roomSystem);
            EditorGUILayout.Space();

            DrawRoomDetectionStatus(roomSystem);
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

        // 新增：房间检测相关按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("显示检测状态"))
        {
            roomSystem.ShowRoomDetectionStatus();
        }

        if (GUILayout.Button("测试检测范围"))
        {
            TestDetectionRange(roomSystem);
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

    // 新增：绘制房间检测设置
    private void DrawRoomDetectionSettings(RoomSystem roomSystem)
    {
        showDetectionSettings = EditorGUILayout.Foldout(showDetectionSettings, "房间检测设置", true);

        if (showDetectionSettings)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUILayout.LabelField($"检测方式: {(roomSystem.useBoxDetection ? "盒形检测" : "圆形检测")}");

            if (roomSystem.useBoxDetection)
            {
                EditorGUILayout.LabelField($"检测尺寸: {roomSystem.roomDetectionSize}");
            }
            else
            {
                EditorGUILayout.LabelField($"检测半径: {roomSystem.roomDetectionRange}");
            }

            EditorGUILayout.LabelField($"检测偏移: {roomSystem.detectionOffset}");
            EditorGUILayout.LabelField($"显示检测边界: {(roomSystem.showDetectionBounds ? "是" : "否")}");
            EditorGUILayout.LabelField($"边界颜色: {roomSystem.detectionBoundsColor}");

            // 快速调整按钮
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("切换检测方式"))
            {
                roomSystem.useBoxDetection = !roomSystem.useBoxDetection;
                EditorUtility.SetDirty(roomSystem);
            }

            if (GUILayout.Button("切换边界显示"))
            {
                roomSystem.showDetectionBounds = !roomSystem.showDetectionBounds;
                EditorUtility.SetDirty(roomSystem);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // 3D偏移快速调整
            EditorGUILayout.LabelField("快速3D偏移调整:", EditorStyles.boldLabel);

            // X轴偏移调整
            EditorGUILayout.LabelField("X轴偏移:", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("X-2"))
            {
                roomSystem.detectionOffset.x -= 2f;
                EditorUtility.SetDirty(roomSystem);
            }
            if (GUILayout.Button("X-1"))
            {
                roomSystem.detectionOffset.x -= 1f;
                EditorUtility.SetDirty(roomSystem);
            }
            if (GUILayout.Button("X-0.5"))
            {
                roomSystem.detectionOffset.x -= 0.5f;
                EditorUtility.SetDirty(roomSystem);
            }
            if (GUILayout.Button("重置X"))
            {
                roomSystem.detectionOffset.x = 0f;
                EditorUtility.SetDirty(roomSystem);
            }
            if (GUILayout.Button("X+0.5"))
            {
                roomSystem.detectionOffset.x += 0.5f;
                EditorUtility.SetDirty(roomSystem);
            }
            if (GUILayout.Button("X+1"))
            {
                roomSystem.detectionOffset.x += 1f;
                EditorUtility.SetDirty(roomSystem);
            }
            if (GUILayout.Button("X+2"))
            {
                roomSystem.detectionOffset.x += 2f;
                EditorUtility.SetDirty(roomSystem);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField($"当前X偏移: {roomSystem.detectionOffset.x:F2}");

            EditorGUILayout.Space();

            // Y轴偏移调整
            EditorGUILayout.LabelField("Y轴偏移:", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Y-2"))
            {
                roomSystem.detectionOffset.y -= 2f;
                EditorUtility.SetDirty(roomSystem);
            }
            if (GUILayout.Button("Y-1"))
            {
                roomSystem.detectionOffset.y -= 1f;
                EditorUtility.SetDirty(roomSystem);
            }
            if (GUILayout.Button("Y-0.5"))
            {
                roomSystem.detectionOffset.y -= 0.5f;
                EditorUtility.SetDirty(roomSystem);
            }
            if (GUILayout.Button("重置Y"))
            {
                roomSystem.detectionOffset.y = 0f;
                EditorUtility.SetDirty(roomSystem);
            }
            if (GUILayout.Button("Y+0.5"))
            {
                roomSystem.detectionOffset.y += 0.5f;
                EditorUtility.SetDirty(roomSystem);
            }
            if (GUILayout.Button("Y+1"))
            {
                roomSystem.detectionOffset.y += 1f;
                EditorUtility.SetDirty(roomSystem);
            }
            if (GUILayout.Button("Y+2"))
            {
                roomSystem.detectionOffset.y += 2f;
                EditorUtility.SetDirty(roomSystem);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField($"当前Y偏移: {roomSystem.detectionOffset.y:F2}");

            EditorGUILayout.Space();

            // Z轴偏移调整
            EditorGUILayout.LabelField("Z轴偏移:", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Z-2"))
            {
                roomSystem.detectionOffset.z -= 2f;
                EditorUtility.SetDirty(roomSystem);
            }
            if (GUILayout.Button("Z-1"))
            {
                roomSystem.detectionOffset.z -= 1f;
                EditorUtility.SetDirty(roomSystem);
            }
            if (GUILayout.Button("Z-0.5"))
            {
                roomSystem.detectionOffset.z -= 0.5f;
                EditorUtility.SetDirty(roomSystem);
            }
            if (GUILayout.Button("重置Z"))
            {
                roomSystem.detectionOffset.z = 0f;
                EditorUtility.SetDirty(roomSystem);
            }
            if (GUILayout.Button("Z+0.5"))
            {
                roomSystem.detectionOffset.z += 0.5f;
                EditorUtility.SetDirty(roomSystem);
            }
            if (GUILayout.Button("Z+1"))
            {
                roomSystem.detectionOffset.z += 1f;
                EditorUtility.SetDirty(roomSystem);
            }
            if (GUILayout.Button("Z+2"))
            {
                roomSystem.detectionOffset.z += 2f;
                EditorUtility.SetDirty(roomSystem);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField($"当前Z偏移: {roomSystem.detectionOffset.z:F2}");

            EditorGUILayout.Space();

            // 快捷重置和复制功能
            EditorGUILayout.LabelField("快捷操作:", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("重置全部"))
            {
                roomSystem.detectionOffset = Vector3.zero;
                EditorUtility.SetDirty(roomSystem);
            }

            if (GUILayout.Button("复制偏移值"))
            {
                EditorGUIUtility.systemCopyBuffer = $"({roomSystem.detectionOffset.x:F2}, {roomSystem.detectionOffset.y:F2}, {roomSystem.detectionOffset.z:F2})";
                Debug.Log($"偏移值已复制到剪贴板: {EditorGUIUtility.systemCopyBuffer}");
            }

            if (GUILayout.Button("对称翻转"))
            {
                roomSystem.detectionOffset = -roomSystem.detectionOffset;
                EditorUtility.SetDirty(roomSystem);
            }

            EditorGUILayout.EndHorizontal();

            // 预设偏移值
            EditorGUILayout.LabelField("常用预设:", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("地面对齐"))
            {
                roomSystem.detectionOffset = new Vector3(0f, -1f, 0f);
                EditorUtility.SetDirty(roomSystem);
            }

            if (GUILayout.Button("中心稍低"))
            {
                roomSystem.detectionOffset = new Vector3(0f, -0.5f, 0f);
                EditorUtility.SetDirty(roomSystem);
            }

            if (GUILayout.Button("向前偏移"))
            {
                roomSystem.detectionOffset = new Vector3(0f, 0f, 2f);
                EditorUtility.SetDirty(roomSystem);
            }

            if (GUILayout.Button("向后偏移"))
            {
                roomSystem.detectionOffset = new Vector3(0f, 0f, -2f);
                EditorUtility.SetDirty(roomSystem);
            }

            EditorGUILayout.EndHorizontal();

            // 显示当前完整偏移值
            EditorGUILayout.Space();
            string offsetInfo = $"当前3D偏移: X={roomSystem.detectionOffset.x:F2}, Y={roomSystem.detectionOffset.y:F2}, Z={roomSystem.detectionOffset.z:F2}";
            EditorGUILayout.LabelField(offsetInfo, EditorStyles.helpBox);

            EditorGUILayout.EndVertical();
        }
    }

    // 新增：绘制房间检测状态
    private void DrawRoomDetectionStatus(RoomSystem roomSystem)
    {
        if (!roomSystem.HasValidPlayer()) return;

        showDetectionStatus = EditorGUILayout.Foldout(showDetectionStatus, "实时检测状态", true);

        if (showDetectionStatus)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);

            // 获取检测信息
            var roomsContainingPlayer = roomSystem.GetRoomsContainingPlayer();
            var depthInfo = roomSystem.GetPlayerDepthInAllRooms();

            EditorGUILayout.LabelField($"玩家位置: {roomSystem.GetPlayer().position.ToString("F2")}");
            EditorGUILayout.LabelField($"当前房间序列: {roomSystem.GetCurrentRoomSequence()}");
            EditorGUILayout.LabelField($"玩家在 {roomsContainingPlayer.Count} 个房间的检测范围内");

            if (roomsContainingPlayer.Count > 1)
            {
                EditorGUILayout.HelpBox("玩家同时在多个房间的检测范围内！可能需要调整检测范围大小。", MessageType.Warning);
            }

            // 滚动区域显示所有房间的检测状态
            detectionStatusScrollPosition = EditorGUILayout.BeginScrollView(detectionStatusScrollPosition, GUILayout.MaxHeight(200));

            var sortedRooms = depthInfo.OrderByDescending(kvp => kvp.Value).ToList();

            foreach (var kvp in sortedRooms)
            {
                int roomSequence = kvp.Key;
                float depth = kvp.Value;

                // 获取房间实例
                RoomSystem.RoomInstance room = null;
                var roomInstances = GetRoomInstances(roomSystem);
                if (roomInstances != null)
                {
                    foreach (var roomObj in roomInstances)
                    {
                        RoomSystem.RoomInstance roomInstance = roomObj as RoomSystem.RoomInstance;
                        if (roomInstance != null && roomInstance.currentSequence == roomSequence)
                        {
                            room = roomInstance;
                            break;
                        }
                    }
                }

                if (room?.gameObject != null)
                {
                    EditorGUILayout.BeginHorizontal();

                    // 状态颜色指示
                    Color originalColor = GUI.backgroundColor;
                    if (roomSequence == roomSystem.GetCurrentRoomSequence())
                        GUI.backgroundColor = Color.green;
                    else if (depth > 0f)
                        GUI.backgroundColor = Color.yellow;
                    else
                        GUI.backgroundColor = Color.white;

                    float distance = room.GetDistanceToPlayer(roomSystem.GetPlayer().position);
                    bool inRange = depth > 0f;

                    string statusText = $"序列{roomSequence}: ";
                    statusText += inRange ? $"✓范围内 深度{depth:F2}" : "范围外";
                    statusText += $" 距离{distance:F2}";

                    if (roomSequence == roomSystem.GetCurrentRoomSequence())
                        statusText += " [当前]";

                    if (GUILayout.Button(statusText, EditorStyles.miniButton))
                    {
                        Selection.activeGameObject = room.gameObject;
                        EditorGUIUtility.PingObject(room.gameObject);
                    }

                    GUI.backgroundColor = originalColor;
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }
    }

    // 获取房间实例列表的辅助方法
    private System.Collections.IList GetRoomInstances(RoomSystem roomSystem)
    {
        var roomInstancesField = typeof(RoomSystem).GetField("roomInstances",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return roomInstancesField?.GetValue(roomSystem) as System.Collections.IList;
    }

    // 新增：测试检测范围
    private void TestDetectionRange(RoomSystem roomSystem)
    {
        if (!roomSystem.HasValidPlayer())
        {
            EditorUtility.DisplayDialog("测试失败", "需要先分配玩家才能测试检测范围。", "确定");
            return;
        }

        Debug.Log("=== 测试房间检测范围（含3D偏移） ===");

        var player = roomSystem.GetPlayer();
        Vector3 originalPos = player.position;

        Debug.Log($"玩家原始位置: {originalPos}");
        Debug.Log($"当前检测偏移: {roomSystem.detectionOffset}");
        Debug.Log($"检测方式: {(roomSystem.useBoxDetection ? "盒形" : "圆形")}");

        // 测试不同位置的检测效果
        Vector3[] testPositions = {
            originalPos + Vector3.right * 3f,        // X+
            originalPos + Vector3.left * 3f,         // X-
            originalPos + Vector3.up * 2f,           // Y+
            originalPos + Vector3.down * 2f,         // Y-
            originalPos + Vector3.forward * 3f,      // Z+
            originalPos + Vector3.back * 3f,         // Z-
            originalPos + roomSystem.detectionOffset, // 偏移中心
            originalPos + roomSystem.detectionOffset + Vector3.right * 2f, // 偏移中心+X
            originalPos + roomSystem.detectionOffset + Vector3.up * 1f,    // 偏移中心+Y
            originalPos + roomSystem.detectionOffset + Vector3.forward * 2f, // 偏移中心+Z
        };

        string[] positionNames = {
            "右侧3米(X+)",
            "左侧3米(X-)",
            "上方2米(Y+)",
            "下方2米(Y-)",
            "前方3米(Z+)",
            "后方3米(Z-)",
            "偏移中心",
            "偏移中心+X",
            "偏移中心+Y",
            "偏移中心+Z"
        };

        for (int i = 0; i < testPositions.Length; i++)
        {
            var testPos = testPositions[i];

            // 临时移动玩家到测试位置
            player.position = testPos;

            var roomsAtPos = roomSystem.GetRoomsContainingPlayer();
            var depthAtPos = roomSystem.GetPlayerDepthInAllRooms();

            Debug.Log($"测试位置[{positionNames[i]}] {testPos}: 在{roomsAtPos.Count}个房间范围内");

            foreach (var room in roomsAtPos)
            {
                float depth = depthAtPos.ContainsKey(room.currentSequence) ? depthAtPos[room.currentSequence] : 0f;
                float roomDistance = room.GetDistanceToPlayer(testPos);
                float detectionDistance = room.GetDistanceToDetectionCenter(testPos, roomSystem.detectionOffset);
                Debug.Log($"  房间序列{room.currentSequence}: 深度{depth:F2}, 房间距离{roomDistance:F2}, 检测距离{detectionDistance:F2}");
            }

            if (roomsAtPos.Count == 0)
            {
                Debug.Log($"  无房间检测到玩家");
            }
        }

        // 恢复玩家原始位置
        player.position = originalPos;

        Debug.Log("检测范围测试完成，玩家位置已恢复");

        // 显示当前状态
        roomSystem.ShowRoomDetectionStatus();

        // 额外的偏移分析
        Debug.Log("=== 偏移分析 ===");
        Debug.Log($"X轴偏移: {roomSystem.detectionOffset.x:F2} (向{'右': '左'})");
        Debug.Log($"Y轴偏移: {roomSystem.detectionOffset.y:F2} (向{'上': '下'})");
        Debug.Log($"Z轴偏移: {roomSystem.detectionOffset.z:F2} (向{'前': '后'})");
        Debug.Log($"总偏移距离: {roomSystem.detectionOffset.magnitude:F2}");
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
        var isInitializedField = typeof(RoomSystem).GetField("isFullyInitialized",
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

            // 房间检测状态
            if (roomSystem.HasValidPlayer())
            {
                var roomsContaining = roomSystem.GetRoomsContainingPlayer();
                EditorGUILayout.LabelField($"检测范围内房间数: {roomsContaining.Count}");

                if (roomsContaining.Count > 1)
                {
                    EditorGUILayout.HelpBox("玩家同时在多个房间范围内！", MessageType.Warning);
                }
            }
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
            roomSystem.ShowRoomDetectionStatus();
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
                var completeInitMethod = typeof(RoomSystem).GetMethod("CompleteFullInitialization",
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
            "基于玩家主动分配的环形房间循环系统 + Bug追踪功能 + 精确房间检测 + 完整3D偏移调整：\n\n" +
            "🎮 玩家分配机制：\n" +
            "• 玩家在实例化时主动向房间系统注册自己的引用\n" +
            "• Player脚本在Awake中查找'RoomSystem(Clone)'对象\n" +
            "• 调用roomSystem.SetPlayer(transform)传递引用\n" +
            "• 房间系统可以等待玩家分配，避免空引用错误\n" +
            "• 提供后备机制：如果等待超时，降级到Tag查找方式\n\n" +
            "🎯 精确房间检测机制：\n" +
            "• 支持盒形和圆形两种检测范围\n" +
            "• 检测范围中心对齐房间GameObject的实际坐标\n" +
            "• 支持完整3D检测偏移（detectionOffset）手动调整检测中心位置\n" +
            "• 使用深度优先选择（选择玩家最深入的房间）\n" +
            "• 提供备用检测逻辑，避免检测失败\n" +
            "• Scene视图实时显示检测边界和状态\n" +
            "• Inspector实时显示玩家在各房间的检测状态\n\n" +
            "🔧 完整3D偏移功能（NEW）：\n" +
            "• detectionOffset: Vector3，完整3D偏移控制\n" +
            "• X轴偏移: 左右调整检测中心（±0.5, ±1, ±2）\n" +
            "• Y轴偏移: 上下调整检测中心（±0.5, ±1, ±2）\n" +
            "• Z轴偏移: 前后调整检测中心（±0.5, ±1, ±2）\n" +
            "• 快捷操作: 重置全部、复制偏移值、对称翻转\n" +
            "• 常用预设: 地面对齐、中心稍低、向前/后偏移\n" +
            "• Scene视图用紫色球体显示检测中心位置\n" +
            "• 灰色线条连接房间中心和检测中心\n" +
            "• 显示房间距离vs检测中心距离的区别\n\n" +
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
            "🔍 检测功能特性：\n" +
            "• 盒形检测：基于3D边界框，适合规整的房间\n" +
            "• 圆形检测：基于半径范围，适合开放的区域\n" +
            "• 深度检测：计算玩家在房间内的深度（0-1）\n" +
            "• 多房间检测：支持玩家同时在多个房间范围内\n" +
            "• 检测边界可视化：Scene视图实时显示检测范围\n" +
            "• 实时状态监控：Inspector显示详细检测信息\n" +
            "• 3D偏移可视化：紫色球体和连线显示检测中心\n\n" +
            "🔗 外部调用接口：\n" +
            "• SetPlayer(Transform) - 设置玩家引用\n" +
            "• GetPlayer() - 获取当前玩家引用\n" +
            "• HasValidPlayer() - 检查是否有有效玩家\n" +
            "• IsWaitingForPlayer() - 检查是否在等待玩家分配\n" +
            "• GetCurrentRoomBugObjects() - 获取当前房间所有有效Bug\n" +
            "• GetCurrentRoomActiveBugs() - 获取当前房间激活Bug\n" +
            "• CurrentRoomHasUnfixedBugs() - 检查是否有未修复Bug\n" +
            "• GetRoomsContainingPlayer() - 获取包含玩家的所有房间\n" +
            "• GetPlayerDepthInAllRooms() - 获取玩家在所有房间的深度\n\n" +
            "🔍 强化调试工具：\n" +
            "• Inspector实时显示玩家分配和连接状态\n" +
            "• 实时显示当前房间有效Bug列表\n" +
            "• 实时显示房间检测状态和深度信息\n" +
            "• XYZ三轴快速调整按钮，实时预览效果\n" +
            "• 常用预设和快捷操作，提高调试效率\n" +
            "• 偏移值复制功能，方便记录和分享设置\n" +
            "• 点击Bug对象可直接选中并定位\n" +
            "• 颜色区分：红色=激活Bug，黄色=修复中，绿色=未激活\n" +
            "• Console输出详细的Bug状态和移动日志\n" +
            "• Scene视图实时显示序列号、距离、深度和Bug统计\n" +
            "• 增强检测范围测试，包含10个测试位置验证XYZ偏移\n" +
            "• 显示房间距离vs检测中心距离的对比分析\n\n" +
            "⚙️ 推荐工作流程：\n" +
            "1. 设置房间预制体列表和参数\n" +
            "2. 调整房间检测范围大小和类型\n" +
            "3. 使用detectionOffset进行3D精确对齐：\n" +
            "   - X轴: 调整左右偏移，对齐房间的实际游戏区域\n" +
            "   - Y轴: 调整上下偏移，匹配玩家的活动高度\n" +
            "   - Z轴: 调整前后偏移，对齐房间的主要活动区域\n" +
            "4. 确保玩家对象有正确的'Player'标签\n" +
            "5. 启用'等待玩家分配'模式\n" +
            "6. 运行游戏，玩家会自动注册到房间系统\n" +
            "7. 观察Inspector中的实时检测状态\n" +
            "8. 使用XYZ快速调整按钮微调检测位置\n" +
            "9. 使用常用预设快速应用典型配置\n" +
            "10. 使用'测试检测范围'验证所有轴向的偏移设置\n" +
            "11. 观察Scene视图中的检测边界和偏移效果\n" +
            "12. 复制偏移值保存最佳配置供后续使用",
            MessageType.Info
        );
    }
}
#endif // UNITY_EDITOR