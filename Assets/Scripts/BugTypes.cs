using System;
using UnityEngine;

namespace BugFixerGame
{
    // 游戏状态枚举
    public enum GameState
    {
        MainMenu,           // 主菜单
        GameStart,          // 游戏开始过程
        MemoryPhase,        // 记忆阶段（观察正确房间）
        TransitionToCheck,  // 过渡到检测阶段
        CheckPhase,         // 检测阶段（寻找bug）
        RoomResult,         // 单个房间结果反馈
        GameEnd,            // 游戏结束
        Paused              // 暂停
    }

    // 重新分类的Bug类型枚举
    public enum BugType
    {
        None,
        ObjectFlickering,           // 物体闪烁
        CollisionMissing,           // 碰撞缺失
        WrongOrMissingMaterial,     // 错误或缺失材质
        WrongObject,                // 错误物体（显示不正确的物体）
        MissingObject,              // 缺失物体（物体消失/透明）
        ObjectShaking,              // 物体震动
        ObjectMovedOrClipping       // 物体位移或穿模
    }

    // 房间配置数据
    [System.Serializable]
    public class RoomConfig
    {
        public int roomId;
        public GameObject normalRoomPrefab;     // 正常房间预制
        public GameObject buggyRoomPrefab;      // 有Bug的房间预制（可选）
        public bool hasBug;                     // 这个房间是否规定有Bug
        public BugType bugType;                 // 规定的Bug类型

        [UnityEngine.TextArea(2, 4)]
        public string bugDescription;           // Bug描述（用于调试）
    }

    // 房间结果类型
    public enum RoomResult
    {
        Perfect,        // 完美（正确识别bug状态）
        Wrong,          // 错误（误判或漏判）
        Timeout         // 超时（如果有时间限制）
    }

    // 游戏结局类型
    public enum GameEnding
    {
        Perfect,        // 7-10分 完美结局
        Good,           // 6分 良好结局  
        Bad             // 0-5分 坏结局
    }

    // 游戏数据类
    [System.Serializable]
    public class GameData
    {
        public GameState stateBeforePause;

        // 可以添加更多需要保存的游戏数据
        public int highScore;
        public int gamesPlayed;
    }
}