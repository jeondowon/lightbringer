namespace Lightbringer.Player
{
    public enum EquipmentKind { LightStaff, HealingStaff, RuneStaff, FoodRing, ManaRing, VitalityRing }

    public static class EquipmentCatalog
    {
        public const int Count = 6;
        public const int Slots = 3;
        public static readonly string[] Names = { "Light Staff", "Healing Staff", "Rune Staff", "Food Ring", "Mana Ring", "Vitality Ring" };
        public static bool IsActive(int id) => id >= 0 && id <= (int)EquipmentKind.RuneStaff;
        public static float Cost(int id) => id == 0 ? 8f : id == 1 ? 25f : 30f;
        public static float Cooldown(int id) => id == 0 ? 0.5f : id == 1 ? 5f : 3f;
    }
}
