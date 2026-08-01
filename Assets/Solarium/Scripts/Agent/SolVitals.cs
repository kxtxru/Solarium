using System;
using UnityEngine;

namespace Solarium
{
    public sealed class SolVitals : MonoBehaviour
    {
        [Header("Health")]
        [Min(1f)] public float maxHealth = 100f;
        [SerializeField] private float currentHealth = 100f;
        [Min(0f)] public float damageCooldown = 0.45f;

        [Header("Energy")]
        [Min(1f)] public float maxEnergy = 100f;
        [SerializeField] private float currentEnergy = 100f;
        [Min(0f)] public float baseEnergyDrainPerSecond = 0.7f;
        [Min(0f)] public float movementEnergyCost = 0.45f;
        [Min(0f)] public float sprintEnergyCost = 1.2f;

        [Header("Movement")]
        [Min(0.1f)] public float movementSpeed = 3.5f;
        [Min(1f)] public float sprintMultiplier = 1.65f;

        private float lastDamageTime = float.NegativeInfinity;

        public float CurrentHealth => currentHealth;
        public float CurrentEnergy => currentEnergy;
        public float NormalizedHealth => maxHealth <= 0f ? 0f : Mathf.Clamp01(currentHealth / maxHealth);
        public float NormalizedEnergy => maxEnergy <= 0f ? 0f : Mathf.Clamp01(currentEnergy / maxEnergy);
        public bool IsAlive { get; private set; } = true;
        public bool IsReceivingDamage { get; private set; }

        public event Action<float> Damaged;
        public event Action<string> Died;

        public void ResetVitals()
        {
            currentHealth = maxHealth;
            currentEnergy = maxEnergy;
            lastDamageTime = float.NegativeInfinity;
            IsReceivingDamage = false;
            IsAlive = true;
        }

        public float TickEnergy(float deltaTime, float movementAmount, bool sprinting, float drainMultiplier)
        {
            if (!IsAlive || deltaTime <= 0f)
                return 0f;

            float rate = baseEnergyDrainPerSecond
                + movementEnergyCost * Mathf.Clamp01(movementAmount)
                + (sprinting ? sprintEnergyCost : 0f);
            float spent = ConsumeEnergy(rate * Mathf.Max(0f, drainMultiplier) * deltaTime);
            IsReceivingDamage = false;
            return spent;
        }

        public float ConsumeEnergy(float amount)
        {
            if (!IsAlive || amount <= 0f)
                return 0f;

            float before = currentEnergy;
            currentEnergy = Mathf.Max(0f, currentEnergy - amount);
            float spent = before - currentEnergy;
            if (currentEnergy <= 0f)
                Kill("energy_depleted");
            return spent;
        }

        public float AddEnergy(float amount)
        {
            if (!IsAlive || amount <= 0f)
                return 0f;

            float before = currentEnergy;
            currentEnergy = Mathf.Min(maxEnergy, currentEnergy + amount);
            return currentEnergy - before;
        }

        public float Heal(float amount)
        {
            if (!IsAlive || amount <= 0f)
                return 0f;

            float before = currentHealth;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            return currentHealth - before;
        }

        public float TakeDamage(float amount, float timestamp)
        {
            if (!IsAlive || amount <= 0f || timestamp - lastDamageTime < damageCooldown)
                return 0f;

            lastDamageTime = timestamp;
            IsReceivingDamage = true;
            float before = currentHealth;
            currentHealth = Mathf.Max(0f, currentHealth - amount);
            float applied = before - currentHealth;
            Damaged?.Invoke(applied);
            if (currentHealth <= 0f)
                Kill("health_depleted");
            return applied;
        }

        public void Kill(string reason)
        {
            if (!IsAlive)
                return;

            IsAlive = false;
            currentHealth = Mathf.Max(0f, currentHealth);
            currentEnergy = Mathf.Max(0f, currentEnergy);
            Died?.Invoke(string.IsNullOrWhiteSpace(reason) ? "unknown" : reason);
        }

        public void SetForTests(float health, float energy)
        {
            currentHealth = Mathf.Clamp(health, 0f, maxHealth);
            currentEnergy = Mathf.Clamp(energy, 0f, maxEnergy);
            IsAlive = currentHealth > 0f && currentEnergy > 0f;
        }
    }
}
