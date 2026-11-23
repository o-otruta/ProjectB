using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using ProjectB.Data;

namespace ProjectB.Meta
{
    public enum TalentNodeState
    {
        Locked = 0,
        Available = 1,
        Unlocked = 2
    }

    public enum TalentUnlockResult
    {
        Success = 0,
        UnknownNode = 1,
        AlreadyUnlocked = 2,
        NotConnected = 3,
        NotEnoughShards = 4,
        NotEnoughCoins = 5,
        CoinsNotAllowed = 6
    }

    /// <summary>
    /// Radial Constellation Talent Tree. Persists unlocks in <see cref="SaveData.unlockedTalentIds"/>,
    /// spends Boss Shards (or coins for the inner ring), grants extra ability slots through
    /// <see cref="IAbilitySlotBonusProvider"/> and gates reactive synergies through <see cref="ISynergyUnlockProvider"/>.
    /// </summary>
    public class TalentTreeManager : IAbilitySlotBonusProvider, ISynergyUnlockProvider, IDisposable
    {
        private static readonly int EffectTypeCount = Enum.GetValues(typeof(TalentEffectType)).Length + 4;

        private readonly SaveManager saveManager;
        private readonly AbilitySlotService slotService;
        private readonly TalentTreeData treeData;
        private readonly Dictionary<string, TalentNodeData> nodesById = new Dictionary<string, TalentNodeData>(StringComparer.Ordinal);
        private readonly HashSet<string> gatedSynergyIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly float[] effectTotals;
        private bool effectsDirty = true;
        private bool disposed;

        public TalentTreeData TreeData => treeData;
        public IReadOnlyCollection<TalentNodeData> Nodes => nodesById.Values;
        public int BossShards => saveManager != null ? saveManager.BossShards : 0;

        public event Action OnSlotBonusesChanged;
        public event Action<TalentNodeData> OnTalentUnlocked;
        public event Action OnTreeChanged;

        [Inject]
        public TalentTreeManager(SaveManager saveManager, AbilitySlotService slotService = null)
            : this(saveManager, Resources.Load<TalentTreeData>(TalentTreeData.RESOURCE_PATH), slotService)
        {
        }

        public TalentTreeManager(SaveManager saveManager, TalentTreeData treeData, AbilitySlotService slotService = null)
        {
            this.saveManager = saveManager;
            this.treeData = treeData;
            this.slotService = slotService;
            effectTotals = new float[Mathf.Max(32, EffectTypeCount)];

            if (treeData != null && treeData.nodes != null)
            {
                for (int i = 0; i < treeData.nodes.Count; i++)
                {
                    TalentNodeData node = treeData.nodes[i];
                    if (node == null || string.IsNullOrEmpty(node.id) || nodesById.ContainsKey(node.id)) continue;
                    nodesById.Add(node.id, node);
                    if (node.nodeType == TalentNodeType.Synergy && !string.IsNullOrEmpty(node.synergyId))
                    {
                        gatedSynergyIds.Add(node.synergyId);
                    }
                }
            }

            if (this.saveManager != null)
            {
                this.saveManager.OnDataChanged += HandleSaveDataChanged;
            }

            slotService?.RegisterBonusProvider(this);
        }

        public TalentNodeData GetNode(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            nodesById.TryGetValue(id, out TalentNodeData node);
            return node;
        }

        public bool IsUnlocked(string id)
        {
            TalentNodeData node = GetNode(id);
            if (node == null) return false;
            if (node.nodeType == TalentNodeType.Origin) return true;
            return saveManager != null && saveManager.IsTalentUnlocked(id);
        }

        public int UnlockedCount
        {
            get
            {
                int count = 0;
                foreach (var kv in nodesById)
                {
                    if (kv.Value.nodeType != TalentNodeType.Origin && IsUnlocked(kv.Key)) count++;
                }
                return count;
            }
        }

        public bool IsConnected(TalentNodeData node)
        {
            if (node == null) return false;
            if (node.nodeType == TalentNodeType.Origin) return true;
            if (node.prerequisiteIds == null || node.prerequisiteIds.Count == 0) return true;

            for (int i = 0; i < node.prerequisiteIds.Count; i++)
            {
                if (IsUnlocked(node.prerequisiteIds[i])) return true;
            }
            return false;
        }

        public TalentNodeState GetState(TalentNodeData node)
        {
            if (node == null) return TalentNodeState.Locked;
            if (IsUnlocked(node.id)) return TalentNodeState.Unlocked;
            return IsConnected(node) ? TalentNodeState.Available : TalentNodeState.Locked;
        }

        public TalentNodeState GetState(string id) => GetState(GetNode(id));

        public TalentUnlockResult CanUnlock(string id, bool payWithCoins = false)
        {
            TalentNodeData node = GetNode(id);
            if (node == null) return TalentUnlockResult.UnknownNode;
            if (IsUnlocked(id)) return TalentUnlockResult.AlreadyUnlocked;
            if (!IsConnected(node)) return TalentUnlockResult.NotConnected;

            if (payWithCoins)
            {
                if (node.coinCost <= 0) return TalentUnlockResult.CoinsNotAllowed;
                int coins = saveManager?.Data != null ? saveManager.Data.coins : 0;
                return coins >= node.coinCost ? TalentUnlockResult.Success : TalentUnlockResult.NotEnoughCoins;
            }

            return BossShards >= node.shardCost ? TalentUnlockResult.Success : TalentUnlockResult.NotEnoughShards;
        }

        public TalentUnlockResult TryUnlock(string id, bool payWithCoins = false)
        {
            TalentUnlockResult check = CanUnlock(id, payWithCoins);
            if (check != TalentUnlockResult.Success) return check;

            TalentNodeData node = GetNode(id);
            bool paid = payWithCoins
                ? saveManager.SpendCoins(node.coinCost, saveImmediately: false)
                : saveManager.SpendBossShards(node.shardCost, saveImmediately: false);
            if (!paid)
            {
                return payWithCoins ? TalentUnlockResult.NotEnoughCoins : TalentUnlockResult.NotEnoughShards;
            }

            bool grantsSlots = node.GetEffectValue(TalentEffectType.BonusActiveSlots) > 0f ||
                               node.GetEffectValue(TalentEffectType.BonusPassiveSlots) > 0f;

            effectsDirty = true;
            saveManager.UnlockTalent(id, saveImmediately: true);

            OnTalentUnlocked?.Invoke(node);
            OnTreeChanged?.Invoke();
            if (grantsSlots)
            {
                OnSlotBonusesChanged?.Invoke();
            }

            return TalentUnlockResult.Success;
        }

        /// <summary>Sum of an effect across all unlocked nodes (cached).</summary>
        public float GetTotalEffect(TalentEffectType type)
        {
            if (effectsDirty) RecalculateEffects();
            int idx = (int)type;
            return idx >= 0 && idx < effectTotals.Length ? effectTotals[idx] : 0f;
        }

        public bool HasEffect(TalentEffectType type) => GetTotalEffect(type) > 0f;

        private void RecalculateEffects()
        {
            Array.Clear(effectTotals, 0, effectTotals.Length);
            foreach (var kv in nodesById)
            {
                TalentNodeData node = kv.Value;
                if (node.effects == null || node.effects.Count == 0) continue;
                if (!IsUnlocked(node.id)) continue;

                for (int i = 0; i < node.effects.Count; i++)
                {
                    int idx = (int)node.effects[i].type;
                    if (idx > 0 && idx < effectTotals.Length)
                    {
                        effectTotals[idx] += node.effects[i].value;
                    }
                }
            }
            effectsDirty = false;
        }

        // --- IAbilitySlotBonusProvider ---

        public int GetBonusActiveSlots() => Mathf.Max(0, Mathf.RoundToInt(GetTotalEffect(TalentEffectType.BonusActiveSlots)));
        public int GetBonusPassiveSlots() => Mathf.Max(0, Mathf.RoundToInt(GetTotalEffect(TalentEffectType.BonusPassiveSlots)));

        // --- ISynergyUnlockProvider ---

        /// <summary>
        /// A synergy that has a node in the tree is unlocked only when that node is unlocked.
        /// Synergies without a node (or a missing tree asset) are never gated.
        /// </summary>
        public bool IsSynergyUnlocked(string synergyId)
        {
            if (string.IsNullOrEmpty(synergyId)) return false;
            if (!gatedSynergyIds.Contains(synergyId)) return true;

            foreach (var kv in nodesById)
            {
                TalentNodeData node = kv.Value;
                if (node.nodeType == TalentNodeType.Synergy && node.synergyId == synergyId && IsUnlocked(node.id))
                {
                    return true;
                }
            }
            return false;
        }

        public TalentNodeData FindSynergyNode(string synergyId)
        {
            if (string.IsNullOrEmpty(synergyId)) return null;
            foreach (var kv in nodesById)
            {
                if (kv.Value.nodeType == TalentNodeType.Synergy && kv.Value.synergyId == synergyId) return kv.Value;
            }
            return null;
        }

        private void HandleSaveDataChanged()
        {
            effectsDirty = true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (saveManager != null)
            {
                saveManager.OnDataChanged -= HandleSaveDataChanged;
            }
            slotService?.UnregisterBonusProvider(this);
        }
    }
}
