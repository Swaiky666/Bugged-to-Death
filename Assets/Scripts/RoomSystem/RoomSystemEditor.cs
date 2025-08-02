#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(RoomSystem))]
public class RoomSystemEditor : Editor
{
    public override void OnInspectorGUI()
    {
        RoomSystem roomSystem = (RoomSystem)target;

        // 绘制默认Inspector
        DrawDefaultInspector();

        EditorGUILayout.Space();
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
        EditorGUILayout.HelpBox(
            "基于距离计算的环形房间循环系统：\n\n" +
            "🔄 环形循环机制：\n" +
            "• 完全移除Trigger，使用纯距离计算检测玩家位置\n" +
            "• 玩家向右移动时：将最左边的房间移动到最右边\n" +
            "• 玩家向左移动时：将最右边的房间移动到最左边\n" +
            "• 每次只移动一个房间，其他房间保持原位置\n" +
            "• 形成一个'环形队列'，房间在环上循环复用\n\n" +
            "🎯 工作原理：\n" +
            "• 系统定期检测玩家与所有房间的距离\n" +
            "• 距离最近的房间即为玩家当前所在房间\n" +
            "• 当玩家接近边界（距离边界房间≤2时）触发房间移动\n" +
            "• 边界房间'传送'到另一端，序列号和位置同步更新\n\n" +
            "📐 数学模型：\n" +
            "• 房间位置 = 序列号 × 房间间距\n" +
            "• 向右移动：最左房间序列号 = 最右房间序列号 + 1\n" +
            "• 向左移动：最右房间序列号 = 最左房间序列号 - 1\n" +
            "• 房间类型 = |序列号| % 预制体数量\n\n" +
            "🎮 视觉效果：\n" +
            "• 玩家看到无限延伸的房间序列\n" +
            "• 房间会在玩家视野边缘'神奇出现'\n" +
            "• 后方房间会'悄悄消失'并重现在前方\n" +
            "• 保持房间预制体的原始外观，不修改颜色\n\n" +
            "⚙️ 关键参数调节：\n" +
            "• Detection Interval: 检测频率，平衡响应速度与性能\n" +
            "• Room Spacing: 影响房间密度和移动触发距离\n" +
            "• Visible Room Count: 同时存在的房间实例数量\n" +
            "• 边界距离阈值: 目前设为2，可根据需要调整\n\n" +
            "🚀 系统优势：\n" +
            "• 内存占用固定，无论玩家移动多远\n" +
            "• 零GC压力，所有房间GameObject重复使用\n" +
            "• 逻辑简单直观，易于理解和调试\n" +
            "• 不依赖物理碰撞，稳定可靠\n" +
            "• 支持无限远距离的探索\n\n" +
            "🔍 调试工具：\n" +
            "• 测试环形移动：演示完整的房间循环过程\n" +
            "• 显示边界房间：查看当前最左/最右房间信息\n" +
            "• Scene视图实时显示序列号和距离\n" +
            "• Console输出详细的房间移动日志",
            MessageType.Info
        );

        // 运行时信息显示
        if (Application.isPlaying)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("运行时状态", EditorStyles.boldLabel);

            // 使用反射获取私有字段信息
            var currentSeqField = typeof(RoomSystem).GetField("currentRoomSequence",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var roomInstancesField = typeof(RoomSystem).GetField("roomInstances",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var lastDetectionField = typeof(RoomSystem).GetField("lastDetectionTime",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (currentSeqField != null && roomInstancesField != null)
            {
                int currentSeq = (int)currentSeqField.GetValue(roomSystem);
                var roomInstances = roomInstancesField.GetValue(roomSystem) as System.Collections.IList;
                float lastDetection = lastDetectionField != null ? (float)lastDetectionField.GetValue(roomSystem) : 0f;

                EditorGUILayout.LabelField($"当前玩家房间序列: {currentSeq}");
                EditorGUILayout.LabelField($"活动房间实例数: {roomInstances?.Count ?? 0}");
                EditorGUILayout.LabelField($"上次检测时间: {lastDetection:F2}s");
                EditorGUILayout.LabelField($"检测间隔: {roomSystem.detectionInterval:F2}s");
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
            }
            EditorGUILayout.EndHorizontal();
        }

        // 如果有修改，标记为脏数据
        if (GUI.changed)
        {
            EditorUtility.SetDirty(roomSystem);
        }
    }
}
#endif