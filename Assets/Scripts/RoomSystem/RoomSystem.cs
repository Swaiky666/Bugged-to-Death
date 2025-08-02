using System.Collections.Generic;
using System.Collections;
using System;
using UnityEngine;

public class RoomSystem : MonoBehaviour
{
    [Header("房间设置")]
    public List<GameObject> roomPrefabs = new List<GameObject>(10);
    public float roomSpacing = 20f;
    public int visibleRoomCount = 10;
    public int playerCenterPosition = 4; // 玩家在可见房间中的目标位置

    [Header("检测设置")]
    public float detectionInterval = 0.1f; // 检测间隔（秒）

    [Header("Debug绘制设置")]
    public Vector3 roomSize = new Vector3(15f, 10f, 15f);
    public Color debugLineColor = Color.green;
    public bool showDebugLines = true;

    [Header("运行时信息")]
    [SerializeField] private int currentRoomSequence = 0;
    [SerializeField] private List<RoomInstance> roomInstances = new List<RoomInstance>();

    [Header("调试信息")]
    [SerializeField] private bool enableDebugLog = true;

    private Transform player;
    private bool isInitialized = false;
    private int lastProcessedSequence = int.MinValue;
    private float lastDetectionTime = 0f;

    [System.Serializable]
    public class RoomInstance
    {
        public GameObject gameObject;
        public int currentSequence;
        public int roomTypeIndex;
        public float worldPosition;

        public RoomInstance(GameObject go, int sequence, int typeIndex, float worldPos)
        {
            gameObject = go;
            currentSequence = sequence;
            roomTypeIndex = typeIndex;
            worldPosition = worldPos;
        }

        public void UpdatePosition(int newSequence, float newWorldPos, List<GameObject> roomPrefabs)
        {
            currentSequence = newSequence;
            worldPosition = newWorldPos;
            roomTypeIndex = Mathf.Abs(newSequence) % roomPrefabs.Count;

            if (gameObject != null)
            {
                gameObject.transform.position = new Vector3(newWorldPos, 0, 0);
                gameObject.name = $"Room_Seq{newSequence}_Type{roomTypeIndex + 1}";
            }
        }

        public float GetDistanceToPlayer(Vector3 playerPos)
        {
            if (gameObject == null) return float.MaxValue;
            return Vector3.Distance(playerPos, gameObject.transform.position);
        }
    }

    void Start()
    {
        InitializeRoomSystem();
    }

    void Update()
    {
        if (!isInitialized || player == null) return;

        // 定期检测玩家位置
        if (Time.time - lastDetectionTime >= detectionInterval)
        {
            CheckPlayerRoomPosition();
            lastDetectionTime = Time.time;
        }
    }

    void InitializeRoomSystem()
    {
        if (roomPrefabs.Count == 0)
        {
            Debug.LogError("房间预制体列表为空！");
            return;
        }

        ClearAllRooms();
        FindPlayer();

        // 确定玩家初始位置对应的序列号
        int playerInitialSequence = GetPlayerSequenceFromPosition();

        Debug.Log($"🎯 玩家初始位置: {(player != null ? player.position.ToString("F2") : "未找到")}, 对应序列: {playerInitialSequence}");

        CreateRoomsAroundSequence(playerInitialSequence);

        currentRoomSequence = playerInitialSequence;
        lastProcessedSequence = playerInitialSequence;

        isInitialized = true;
        Debug.Log($"房间系统初始化完成，以序列 {playerInitialSequence} 为中心创建了 {roomInstances.Count} 个房间");

        LogCurrentRoomLayout("初始化完成");
    }

    void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            Debug.Log($"找到玩家对象: {playerObj.name}, 位置: {player.position}");
        }
        else
        {
            Debug.LogWarning("找不到标签为 'Player' 的对象");
        }
    }

    int GetPlayerSequenceFromPosition()
    {
        if (player == null) return 0;

        // 直接根据玩家的X坐标计算序列号
        float playerX = player.position.x;
        int sequence = Mathf.RoundToInt(playerX / roomSpacing);

        Debug.Log($"玩家X坐标: {playerX:F2}, 房间间距: {roomSpacing}, 计算序列: {sequence}");
        return sequence;
    }

    void CheckPlayerRoomPosition()
    {
        if (player == null) return;

        // 找到距离玩家最近的房间
        RoomInstance closestRoom = null;
        float closestDistance = float.MaxValue;

        foreach (var room in roomInstances)
        {
            if (room.gameObject != null)
            {
                float distance = room.GetDistanceToPlayer(player.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestRoom = room;
                }
            }
        }

        if (closestRoom != null && closestRoom.currentSequence != currentRoomSequence)
        {
            // 玩家进入了新房间
            OnPlayerEnterRoom(closestRoom.currentSequence);
        }
    }

    void CreateRoomsAroundSequence(int centerSequence)
    {
        // 计算房间序列范围，以centerSequence为中心
        int startSequence = centerSequence - playerCenterPosition;

        Debug.Log($"创建房间范围: {startSequence} 到 {startSequence + visibleRoomCount - 1}");

        for (int i = 0; i < visibleRoomCount; i++)
        {
            int sequenceNumber = startSequence + i;
            CreateRoomAtSequence(sequenceNumber);
        }
    }

    void CreateRoomAtSequence(int sequenceNumber)
    {
        // 根据序列号计算世界位置（确保完全对应）
        float worldPos = sequenceNumber * roomSpacing;
        int roomTypeIndex = Mathf.Abs(sequenceNumber) % roomPrefabs.Count;

        GameObject roomPrefab = roomPrefabs[roomTypeIndex];
        if (roomPrefab == null)
        {
            Debug.LogError($"房间预制体 {roomTypeIndex} 为空！");
            return;
        }

        GameObject roomInstance = Instantiate(roomPrefab, transform);
        roomInstance.name = $"Room_Seq{sequenceNumber}_Type{roomTypeIndex + 1}";
        roomInstance.transform.position = new Vector3(worldPos, 0, 0);

        RoomInstance newRoom = new RoomInstance(roomInstance, sequenceNumber, roomTypeIndex, worldPos);
        roomInstances.Add(newRoom);

        Debug.Log($"创建房间: 序列{sequenceNumber}, 位置({worldPos:F1}, 0, 0), 类型{roomTypeIndex + 1}");
    }

    void ClearAllRooms()
    {
        foreach (var room in roomInstances)
        {
            if (room.gameObject != null)
                DestroyImmediate(room.gameObject);
        }
        roomInstances.Clear();
    }

    public void OnPlayerEnterRoom(int sequenceNumber)
    {
        // 防止重复处理相同序列
        if (sequenceNumber == lastProcessedSequence)
        {
            return;
        }

        int previousSequence = currentRoomSequence;
        currentRoomSequence = sequenceNumber;

        // 计算移动方向
        int direction = sequenceNumber - previousSequence;

        Debug.Log($"🎯 玩家从序列 {previousSequence} 移动到序列 {sequenceNumber}，方向：{(direction > 0 ? "右" : "左")}");

        // 执行环形房间移动
        if (direction > 0)
        {
            // 向右移动：将最左边的房间移动到最右边
            HandleMovementRight(sequenceNumber);
        }
        else if (direction < 0)
        {
            // 向左移动：将最右边的房间移动到最左边
            HandleMovementLeft(sequenceNumber);
        }

        lastProcessedSequence = sequenceNumber;
        LogCurrentRoomLayout($"玩家移动到序列 {sequenceNumber} 后");
    }

    void HandleMovementRight(int currentSeq)
    {
        // 玩家向右移动，检查是否需要将左边房间移到右边
        RoomInstance leftmostRoom = GetLeftmostRoom();
        RoomInstance rightmostRoom = GetRightmostRoom();

        if (leftmostRoom == null || rightmostRoom == null) return;

        // 检查玩家是否接近右边界，需要移动房间
        int distanceToRightEdge = rightmostRoom.currentSequence - currentSeq;

        if (distanceToRightEdge <= 2) // 距离右边界2个房间时移动
        {
            MoveLeftmostRoomToRight();
            Debug.Log($"🔄 向右移动：将最左房间(序列{leftmostRoom.currentSequence})移动到最右边");
        }
    }

    void HandleMovementLeft(int currentSeq)
    {
        // 玩家向左移动，检查是否需要将右边房间移到左边
        RoomInstance leftmostRoom = GetLeftmostRoom();
        RoomInstance rightmostRoom = GetRightmostRoom();

        if (leftmostRoom == null || rightmostRoom == null) return;

        // 检查玩家是否接近左边界，需要移动房间
        int distanceToLeftEdge = currentSeq - leftmostRoom.currentSequence;

        if (distanceToLeftEdge <= 2) // 距离左边界2个房间时移动
        {
            MoveRightmostRoomToLeft();
            Debug.Log($"🔄 向左移动：将最右房间(序列{rightmostRoom.currentSequence})移动到最左边");
        }
    }

    void MoveLeftmostRoomToRight()
    {
        RoomInstance leftmostRoom = GetLeftmostRoom();
        RoomInstance rightmostRoom = GetRightmostRoom();

        if (leftmostRoom == null || rightmostRoom == null) return;

        // 计算新位置：最右边房间的下一个位置
        int newSequence = rightmostRoom.currentSequence + 1;
        float newWorldPos = newSequence * roomSpacing;

        Debug.Log($"环形移动：序列{leftmostRoom.currentSequence}(位置{leftmostRoom.worldPosition:F1}) → 序列{newSequence}(位置{newWorldPos:F1})");

        // 更新房间位置和数据
        leftmostRoom.UpdatePosition(newSequence, newWorldPos, roomPrefabs);
        UpdateRoomAppearance(leftmostRoom);
    }

    void MoveRightmostRoomToLeft()
    {
        RoomInstance leftmostRoom = GetLeftmostRoom();
        RoomInstance rightmostRoom = GetRightmostRoom();

        if (leftmostRoom == null || rightmostRoom == null) return;

        // 计算新位置：最左边房间的前一个位置
        int newSequence = leftmostRoom.currentSequence - 1;
        float newWorldPos = newSequence * roomSpacing;

        Debug.Log($"环形移动：序列{rightmostRoom.currentSequence}(位置{rightmostRoom.worldPosition:F1}) → 序列{newSequence}(位置{newWorldPos:F1})");

        // 更新房间位置和数据
        rightmostRoom.UpdatePosition(newSequence, newWorldPos, roomPrefabs);
        UpdateRoomAppearance(rightmostRoom);
    }

    void UpdateRoomAppearance(RoomInstance room)
    {
        // 保持房间原有外观，不修改颜色
        // 如果需要其他外观更新（如激活/禁用某些组件），可以在这里添加

        // 例如：更新房间名称以反映新的序列号
        if (room.gameObject != null)
        {
            room.gameObject.name = $"Room_Seq{room.currentSequence}_Type{room.roomTypeIndex + 1}";
        }
    }

    RoomInstance GetLeftmostRoom()
    {
        if (roomInstances.Count == 0) return null;

        RoomInstance leftmost = roomInstances[0];
        foreach (var room in roomInstances)
        {
            if (room.currentSequence < leftmost.currentSequence)
                leftmost = room;
        }
        return leftmost;
    }

    RoomInstance GetRightmostRoom()
    {
        if (roomInstances.Count == 0) return null;

        RoomInstance rightmost = roomInstances[0];
        foreach (var room in roomInstances)
        {
            if (room.currentSequence > rightmost.currentSequence)
                rightmost = room;
        }
        return rightmost;
    }

    void LogCurrentRoomLayout(string context)
    {
        var sortedRooms = new List<RoomInstance>(roomInstances);
        sortedRooms.Sort((a, b) => a.currentSequence.CompareTo(b.currentSequence));

        string layout = "";
        for (int i = 0; i < sortedRooms.Count; i++)
        {
            var room = sortedRooms[i];
            string roomLabel = $"S{room.currentSequence}T{room.roomTypeIndex + 1}";

            if (room.currentSequence == currentRoomSequence)
            {
                layout += $"[{roomLabel}] ";
            }
            else
            {
                layout += $"{roomLabel} ";
            }
        }

        Debug.Log($"📍 {context}");
        Debug.Log($"房间布局: {layout}");
        Debug.Log($"玩家在序列 {currentRoomSequence}");
    }

    // ==================== 编辑器方法 ====================

    [ContextMenu("重新初始化房间系统")]
    public void EditorInitializeRooms()
    {
        InitializeRoomSystem();
    }

    [ContextMenu("强制检测玩家位置")]
    public void ForceCheckPlayerPosition()
    {
        if (player == null) FindPlayer();

        if (player != null)
        {
            Debug.Log("=== 强制检测玩家位置 ===");
            Debug.Log($"玩家当前世界坐标: {player.position}");

            // 找到最近的房间
            RoomInstance closestRoom = null;
            float closestDistance = float.MaxValue;

            foreach (var room in roomInstances)
            {
                if (room.gameObject != null)
                {
                    float distance = room.GetDistanceToPlayer(player.position);
                    Debug.Log($"房间序列{room.currentSequence}: 位置{room.gameObject.transform.position:F1}, 距离{distance:F2}");

                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestRoom = room;
                    }
                }
            }

            if (closestRoom != null)
            {
                Debug.Log($"✓ 最近的房间: 序列{closestRoom.currentSequence}, 距离{closestDistance:F2}");

                if (closestRoom.currentSequence != currentRoomSequence)
                {
                    Debug.Log($"⚠️ 位置不匹配！当前记录序列: {currentRoomSequence}, 最近房间序列: {closestRoom.currentSequence}");
                    currentRoomSequence = closestRoom.currentSequence;
                    lastProcessedSequence = closestRoom.currentSequence;
                    Debug.Log($"✓ 已更正玩家位置为序列 {closestRoom.currentSequence}");
                }
                else
                {
                    Debug.Log($"✅ 玩家位置正确，在序列 {closestRoom.currentSequence}");
                }
            }

            LogCurrentRoomLayout("检测后的状态");
        }
        else
        {
            Debug.LogWarning("没有找到玩家对象，请确保场景中有Tag为'Player'的对象。");
        }
    }

    [ContextMenu("验证房间位置")]
    public void VerifyRoomPositions()
    {
        Debug.Log("=== 验证房间位置 ===");
        if (player != null)
        {
            Debug.Log($"玩家当前位置: {player.position}");
            float playerX = player.position.x;
            int expectedSequence = Mathf.RoundToInt(playerX / roomSpacing);
            Debug.Log($"根据位置计算的期望序列: {expectedSequence}");
            Debug.Log($"当前记录的序列: {currentRoomSequence}");
            Debug.Log($"匹配状态: {(expectedSequence == currentRoomSequence ? "✅ 匹配" : "❌ 不匹配")}");
            Debug.Log("---");
        }

        foreach (var room in roomInstances)
        {
            if (room.gameObject != null)
            {
                float expectedPos = room.currentSequence * roomSpacing;
                float actualPos = room.gameObject.transform.position.x;
                bool isCorrect = Mathf.Abs(expectedPos - actualPos) < 0.01f;

                Debug.Log($"序列{room.currentSequence}: 期望位置{expectedPos:F1}, 实际位置{actualPos:F1} {(isCorrect ? "✓" : "❌")}");
            }
        }
    }

    [ContextMenu("显示距离信息")]
    public void ShowDistanceInfo()
    {
        if (player == null)
        {
            Debug.LogWarning("找不到玩家对象");
            return;
        }

        Debug.Log("=== 房间距离信息 ===");
        Debug.Log($"玩家位置: {player.position}");

        var sortedByDistance = new List<RoomInstance>(roomInstances);
        sortedByDistance.Sort((a, b) => a.GetDistanceToPlayer(player.position).CompareTo(b.GetDistanceToPlayer(player.position)));

        for (int i = 0; i < sortedByDistance.Count; i++)
        {
            var room = sortedByDistance[i];
            if (room.gameObject != null)
            {
                float distance = room.GetDistanceToPlayer(player.position);
                string marker = i == 0 ? "🎯" : "  ";
                Debug.Log($"{marker} 排名{i + 1}: 序列{room.currentSequence}, 位置{room.gameObject.transform.position:F1}, 距离{distance:F2}");
            }
        }
    }

    [ContextMenu("模拟向右移动")]
    public void TestMoveRight()
    {
        Debug.Log("🧪 模拟玩家向右移动");
        OnPlayerEnterRoom(currentRoomSequence + 1);
    }

    [ContextMenu("模拟向左移动")]
    public void TestMoveLeft()
    {
        Debug.Log("🧪 模拟玩家向左移动");
        OnPlayerEnterRoom(currentRoomSequence - 1);
    }

    [ContextMenu("显示当前布局")]
    public void LogCurrentLayout()
    {
        LogCurrentRoomLayout("手动查看当前布局");
    }

    [ContextMenu("显示边界房间信息")]
    public void ShowBoundaryRooms()
    {
        Debug.Log("=== 边界房间信息 ===");

        var leftmost = GetLeftmostRoom();
        var rightmost = GetRightmostRoom();

        if (leftmost != null)
        {
            Debug.Log($"最左房间: 序列{leftmost.currentSequence}, 位置{leftmost.worldPosition:F1}");
        }
        else
        {
            Debug.Log("找不到最左房间");
        }

        if (rightmost != null)
        {
            Debug.Log($"最右房间: 序列{rightmost.currentSequence}, 位置{rightmost.worldPosition:F1}");
        }
        else
        {
            Debug.Log("找不到最右房间");
        }

        if (leftmost != null && rightmost != null)
        {
            int totalSpan = rightmost.currentSequence - leftmost.currentSequence + 1;
            Debug.Log($"房间跨度: {totalSpan} (序列{leftmost.currentSequence}到{rightmost.currentSequence})");
        }
    }

    [ContextMenu("测试环形移动")]
    public void TestRingMovement()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("房间系统未初始化");
            return;
        }

        Debug.Log("=== 🧪 测试环形移动功能 ===");

        // 记录移动前状态
        Debug.Log("移动前状态:");
        LogCurrentRoomLayout("测试前");
        ShowBoundaryRooms();

        // 模拟向右移动
        Debug.Log("\n🚀 模拟向右移动，应该将最左房间移到最右...");
        int newSequence = currentRoomSequence + 3; // 移动3步，确保触发房间移动
        OnPlayerEnterRoom(newSequence);

        Debug.Log("移动后状态:");
        LogCurrentRoomLayout("测试后");
        ShowBoundaryRooms();

        Debug.Log("=== 环形移动测试完成 ===");
    }

    // ==================== Gizmos绘制 ====================

    void OnDrawGizmos()
    {
        if (!showDebugLines || !isInitialized) return;

        Gizmos.color = debugLineColor;

        foreach (var room in roomInstances)
        {
            if (room.gameObject != null)
            {
                Vector3 center = room.gameObject.transform.position;
                DrawRoomBounds(center, roomSize);

                // 绘制序列号和距离信息
#if UNITY_EDITOR
                Vector3 labelPos = center + Vector3.up * (roomSize.y * 0.5f + 2f);
                string label = $"Seq{room.currentSequence}\nPos{center.x:F1}";

                if (player != null)
                {
                    float distance = room.GetDistanceToPlayer(player.position);
                    label += $"\nDist{distance:F1}";
                }

                UnityEditor.Handles.Label(labelPos, label);
#endif

                if (room.currentSequence == currentRoomSequence)
                {
                    Gizmos.color = Color.yellow;
                    DrawRoomBounds(center, roomSize * 1.1f);
                    Gizmos.color = debugLineColor;
                }
            }
        }

        // 绘制玩家位置
        if (player != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(player.position, 1f);
        }
    }

    void DrawRoomBounds(Vector3 center, Vector3 size)
    {
        Vector3 halfSize = size * 0.5f;

        // 绘制立方体框架
        Vector3[] corners = new Vector3[8];
        corners[0] = center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z);
        corners[1] = center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z);
        corners[2] = center + new Vector3(halfSize.x, -halfSize.y, halfSize.z);
        corners[3] = center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z);
        corners[4] = center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z);
        corners[5] = center + new Vector3(halfSize.x, halfSize.y, -halfSize.z);
        corners[6] = center + new Vector3(halfSize.x, halfSize.y, halfSize.z);
        corners[7] = center + new Vector3(-halfSize.x, halfSize.y, halfSize.z);

        // 底面
        Gizmos.DrawLine(corners[0], corners[1]);
        Gizmos.DrawLine(corners[1], corners[2]);
        Gizmos.DrawLine(corners[2], corners[3]);
        Gizmos.DrawLine(corners[3], corners[0]);

        // 顶面
        Gizmos.DrawLine(corners[4], corners[5]);
        Gizmos.DrawLine(corners[5], corners[6]);
        Gizmos.DrawLine(corners[6], corners[7]);
        Gizmos.DrawLine(corners[7], corners[4]);

        // 竖直边
        Gizmos.DrawLine(corners[0], corners[4]);
        Gizmos.DrawLine(corners[1], corners[5]);
        Gizmos.DrawLine(corners[2], corners[6]);
        Gizmos.DrawLine(corners[3], corners[7]);
    }
}