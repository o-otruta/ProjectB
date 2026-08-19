using System;
using UnityEngine;

namespace ProjectB.Data.Meta
{
    public enum DailyBonusRewardType
    {
        Coins,
        Ability
    }

    [Serializable]
    public struct DailyBonusReward
    {
        public DailyBonusRewardType rewardType;
        
        [Header("If Coins")]
        public int coinsAmount;
        
        [Header("If Ability")]
        public string abilityId;
        
        [Header("Visual")]
        public Sprite icon;
    }
}
