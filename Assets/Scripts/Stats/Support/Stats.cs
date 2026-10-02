using System;
using System.Collections.Generic;
using UnityEngine;

namespace Prototype
{
    [Serializable]
    public class Stat
    {
        [SerializeField] private StatType type;
        [SerializeField] private float value;

        public StatType Type => type;
        public float Value
        {
            get => value;
            set => this.value = value;
        }

        public Stat(StatType type, float value)
        {
            this.type = type;
            this.value = value;
        }
    }

    [Serializable]
    public class Energy
    {
        [SerializeField] private EnergyType type;
        [SerializeField] private float maxValue;
        [SerializeField] private float curValue;

        public EnergyType Type => type;
        public float MaxValue => maxValue;
        public float CurValue => curValue;
        public float Ratio => maxValue <= 0f ? 0f : curValue / maxValue;
        public bool IsEmpty => curValue <= 0f;
        public bool IsFull => curValue >= maxValue;

        public event Action<Energy> OnChanged;

        public Energy(EnergyType type, float maxValue)
        {
            this.type = type;
            this.maxValue = maxValue;
            curValue = maxValue;
        }

        public void Lose(float loss)
        {
            if (loss <= 0f) return;
            curValue = Mathf.Max(0f, curValue - loss);
            OnChanged?.Invoke(this);
        }

        public void Recover(float recovery)
        {
            if (recovery <= 0f) return;
            curValue = Mathf.Min(maxValue, curValue + recovery);
            OnChanged?.Invoke(this);
        }

        public bool TrySpend(float cost)
        {
            if (curValue < cost) return false;
            Lose(cost);
            return true;
        }

        /// <summary>
        /// 비율로 현재값을 맞춘다. 스테이지를 넘어온 체력을 복원할 때 쓴다
        /// (<see cref="PartyState"/>).
        ///
        /// <b>절대값이 아니라 비율인 이유</b>는 최대치가 변하기 때문이다 —
        /// 레벨업이 최대 체력을 올리면 지난 스테이지의 절대값은 의미를 잃는다.
        /// </summary>
        public void SetRatio(float ratio)
        {
            curValue = Mathf.Clamp01(ratio) * maxValue;
            OnChanged?.Invoke(this);
        }

        public void SetMax(float newMax, bool refill = false)
        {
            maxValue = Mathf.Max(0f, newMax);
            curValue = refill ? maxValue : Mathf.Min(curValue, maxValue);
            OnChanged?.Invoke(this);
        }
    }

    [Serializable]
    public class Stats
    {
        [SerializeField] private List<Stat> stats = new List<Stat>();

        public Stat GetStat(StatType type)
        {
            for (int i = 0; i < stats.Count; i++)
                if (stats[i].Type == type)
                    return stats[i];
            return null;
        }

        public float GetValue(StatType type, float fallback = 0f)
        {
            Stat s = GetStat(type);
            return s != null ? s.Value : fallback;
        }

        public void Set(StatType type, float value)
        {
            Stat s = GetStat(type);
            if (s != null) s.Value = value;
            else stats.Add(new Stat(type, value));
        }
    }

    [Serializable]
    public class Energies
    {
        [SerializeField] private List<Energy> energies = new List<Energy>();

        public Energy GetEnergy(EnergyType type)
        {
            for (int i = 0; i < energies.Count; i++)
                if (energies[i].Type == type)
                    return energies[i];
            return null;
        }

        public Energy Ensure(EnergyType type, float maxValue)
        {
            Energy e = GetEnergy(type);
            if (e != null) return e;
            e = new Energy(type, maxValue);
            energies.Add(e);
            return e;
        }
    }
}
