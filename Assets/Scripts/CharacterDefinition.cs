using System;
using UnityEngine;

namespace WorldEvolution
{
    /// <summary>
    /// One character in the game. A CharacterDefinition is a
    /// ScriptableObject asset that knows:
    ///   - the 16 direction sprites
    ///   - the 16 sway masks
    ///   - the eye / hair / skin tint
    ///   - the body proportions (height, shoulder width, head size)
    ///     so the same shader works for children, adults, etc.
    ///   - the age stage (toddler, child, teen, young adult, adult,
    ///     old) so a single runtime can swap definitions as the
    ///     character ages
    ///   - the gender (male / female) for future variants
    ///
    /// This is the data model for the future character-customization
    /// system. The existing BillboardCharacter reads only the
    /// sprites + masks for now; the rest of the fields are unused
    /// until you wire them up. They are present so future code can
    /// be added without breaking the existing setup.
    /// </summary>
    [CreateAssetMenu(menuName = "World Evolution/Character Definition",
                     fileName = "Character_",
                     order = 100)]
    public class CharacterDefinition : ScriptableObject
    {
        public enum Gender { Male, Female }
        public enum AgeStage
        {
            Toddler,      // 0-3
            Child,        // 4-9
            Teen,         // 10-17
            YoungAdult,   // 18-29
            Adult,        // 30-49
            Old,          // 50+
        }

        [Header("Identity")]
        public string characterId = "caveman_default";
        public Gender gender = Gender.Male;
        public AgeStage ageStage = AgeStage.Adult;

        [Header("Sprites — turntable order")]
        [Tooltip("Front, then rotating around the character " +
                 "(00_front, 01_front_right_slight, ... 15_front_left_slight). " +
                 "All sprites MUST be the same pixel size and NOT packed into a sprite atlas.")]
        public Sprite[] directionSprites;

        [Header("Sway masks (same order as sprites)")]
        [Tooltip("R = hair, G = cloth/loincloth, B = torso/breathing, A = head. " +
                 "Leave empty to auto-load from Resources/CharacterMasks/.")]
        public Texture2D[] directionMasks;

        [Header("Body proportions (in sprite units)")]
        [Tooltip("Used by the shader to scale the body-bob, breathing, and " +
                 "stride amplitudes correctly across age stages. The default " +
                 "values are calibrated for the shipped adult caveman sprite " +
                 "(176x392). Set to 0.45 for a child, 0.55 for a teen, " +
                 "0.95 for an old hunched character, etc.")]
        [Range(0.3f, 1.2f)] public float bodyScale = 1.0f;

        [Tooltip("Multiplier for the head size on this character. " +
                 "Children's heads are proportionally larger (~1.15); " +
                 "adult heads are 1.0; old heads are slightly larger (~1.05).")]
        [Range(0.7f, 1.4f)] public float headSize = 1.0f;

        [Header("Colours")]
        [Tooltip("Multiplicative tint applied to skin pixels only " +
                 "(the shader uses a luminance test to identify skin).")]
        public Color skinTint = Color.white;

        [Tooltip("Multiplicative tint applied to hair pixels only.")]
        public Color hairTint = new Color(0.45f, 0.32f, 0.20f);

        [Tooltip("Multiplicative tint applied to eye pixels (the " +
                 "two darkest spots on the face).")]
        public Color eyeTint = new Color(0.20f, 0.12f, 0.08f);

        [Tooltip("Multiplicative tint applied to the cloth/leather " +
                 "(the brown loincloth and cross-strap).")]
        public Color clothTint = new Color(0.75f, 0.55f, 0.35f);

        [Header("Hair / beard (for the haircut + growth system)")]
        [Tooltip("Current hair length, in fractions of a full head of hair. " +
                 "1.0 = the artist's full hair; 0 = shaved. Used by the " +
                 "haircut system to fade the hair on the sprite.")]
        [Range(0f, 1.2f)] public float hairLength = 1.0f;

        [Tooltip("Current beard length, same scale as hairLength.")]
        [Range(0f, 1.2f)] public float beardLength = 0.5f;

        [Tooltip("How fast the hair grows per in-game day. 0.01 ≈ a 1% " +
                 "growth per day = visibly long in a couple of months.")]
        [Range(0f, 0.05f)] public float hairGrowthPerDay = 0.01f;

        [Tooltip("How fast the beard grows. Usually about 1.5x the hair rate.")]
        [Range(0f, 0.1f)] public float beardGrowthPerDay = 0.015f;

        [Header("Animator (which animations the character can do)")]
        [Tooltip("If false, the haircut / shave UI is hidden. NPCs that " +
                 "never get a haircut should set this to false.")]
        public bool allowHaircut = true;

        [Tooltip("If false, the character cannot grow a beard. Used for " +
                 "young characters (toddler / child / teen) and for some " +
                 "female variants.")]
        public bool canHaveBeard = true;
    }
}
