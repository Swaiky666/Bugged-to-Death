// 重构后的 BugTypes.cs（仅保留需要的枚举）
using System;
using UnityEngine;

namespace BugFixerGame
{
    // 简化后的游戏状态（只保留主菜单和暂停）
    public enum GameState
    {
        MainMenu,
        Paused
    }

    // 重新分类的Bug类型枚举
    public enum BugType
    {
        None,
        ObjectFlickering,
        CollisionMissing,
        WrongOrMissingMaterial,
        WrongObject,
        MissingObject,
        ObjectShaking,
        ObjectMovedOrClipping
    }

    // 游戏数据类
    [System.Serializable]
    public class GameData
    {
        public GameState stateBeforePause;
        public int highScore;
        public int gamesPlayed;
    }
}
