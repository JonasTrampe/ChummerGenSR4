namespace Chummer.Core
{
    public enum CapacityStyle
    {
        Zero = 0,
        Standard = 1,
        PerRating = 2
    }

    public enum QualityType
    {
        Positive = 0,
        Negative = 1
    }

    public enum QualitySource
    {
        Selected = 0,
        Metatype = 1,
        MetatypeRemovable = 2
    }

    public enum SpiritType
    {
        Spirit = 0,
        Sprite = 1
    }

    public enum ContactType
    {
        Contact = 0,
        Enemy = 1,
        Pet = 2
    }

    public enum LifestyleType
    {
        Standard = 0,
        BoltHole = 1,
        Safehouse = 2,
        Advanced = 3
    }

    /// <summary>
    /// Ported verbatim from clsImprovement.cs's ImprovementType - the member names are the exact
    /// strings the legacy save format writes for &lt;improvementttype&gt; (sic - the tag itself has
    /// the typo), so Enum.TryParse round-trips real save files without a translation table.
    /// </summary>
    public enum ImprovementType
    {
        Skill = 0,
        Attribute = 1,
        Text = 2,
        BallisticArmor = 3,
        ImpactArmor = 4,
        Reach = 5,
        Nuyen = 6,
        Essence = 7,
        Reaction = 8,
        PhysicalCm = 9,
        StunCm = 10,
        UnarmedDv = 11,
        SkillGroup = 12,
        SkillCategory = 13,
        SkillAttribute = 14,
        InitiativePass = 15,
        MatrixInitiative = 16,
        MatrixInitiativePass = 17,
        LifestyleCost = 18,
        CmThreshold = 19,
        EnhancedArticulation = 20,
        WeaponCategoryDv = 21,
        CyberwareEssCost = 22,
        SpecialTab = 23,
        Initiative = 24,
        Uneducated = 25,
        LivingPersonaResponse = 26,
        LivingPersonaSignal = 27,
        LivingPersonaFirewall = 28,
        LivingPersonaSystem = 29,
        LivingPersonaBiofeedback = 30,
        Smartlink = 31,
        BiowareEssCost = 32,
        GenetechCostMultiplier = 33,
        BasicBiowareEssCost = 34,
        TransgenicsBiowareCost = 35,
        SoftWeave = 36,
        SensitiveSystem = 37,
        ConditionMonitor = 38,
        UnarmedDvPhysical = 39,
        MovementPercent = 40,
        Adapsin = 41,
        FreePositiveQualities = 42,
        FreeNegativeQualities = 43,
        NuyenMaxBp = 44,
        CmOverflow = 45,
        FreeSpiritPowerPoints = 46,
        AdeptPowerPoints = 47,
        ArmorEncumbrancePenalty = 48,
        Uncouth = 49,
        Initiation = 50,
        Submersion = 51,
        Infirm = 52,
        Skillwire = 53,
        DamageResistance = 54,
        RestrictedItemCount = 55,
        AdeptLinguistics = 56,
        SwimPercent = 57,
        FlyPercent = 58,
        FlySpeed = 59,
        JudgeIntentions = 60,
        LiftAndCarry = 61,
        Memory = 62,
        Concealability = 63,
        SwapSkillAttribute = 64,
        DrainResistance = 65,
        FadingResistance = 66,
        MatrixInitiativePassAdd = 67,
        InitiativePassAdd = 68,
        Composure = 69,
        UnarmedAp = 70,
        CmThresholdOffset = 71,
        Restricted = 72,
        Notoriety = 73,
        SpellCategory = 74,
        ThrowRange = 75,
        SkillsoftAccess = 76,
        AddSprite = 77,
        BlackMarketDiscount = 78,
        SelectWeapon = 79,
        ComplexFormLimit = 80,
        SpellLimit = 81,
        QuickeningMetamagic = 82,
        BasicLifestyleCost = 83,
        ThrowStr = 84,
        IgnoreCmPenaltyStun = 85,
        IgnoreCmPenaltyPhysical = 86,
        CyborgEssence = 87,
        EssenceMax = 88,
        SelectSenseware = 89,
    }

    /// <summary>Ported verbatim from clsImprovement.cs's ImprovementSource - see <see cref="ImprovementType"/>.</summary>
    public enum ImprovementSource
    {
        Quality = 0,
        Power = 1,
        Metatype = 2,
        Cyberware = 3,
        Metavariant = 4,
        Bioware = 5,
        Nanotech = 6,
        Genetech = 7,
        ArmorEncumbrance = 8,
        Gear = 9,
        Spell = 10,
        MartialArtAdvantage = 11,
        Initiation = 12,
        Submersion = 13,
        Metamagic = 14,
        Echo = 15,
        Armor = 16,
        ArmorMod = 17,
        EssenceLoss = 18,
        ConditionMonitor = 19,
        CritterPower = 20,
        ComplexForm = 21,
        EdgeUse = 22,
        MutantCritter = 23,
        Cyberzombie = 24,
        StackedFocus = 25,
        AttributeLoss = 26,
        Custom = 999,
    }
}