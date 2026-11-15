using UnityEngine;

/// <summary>
/// Dokunmatik ekran kontrol durumu. MobileControls tarafından güncellenir,
/// diğer scriptler (PlayerController, PlayerCombat vb.) buradan okur.
/// Mobil olmayan platformlarda her şey varsayılan (0 / false) döner.
/// </summary>
public static class MobileInput
{
    public static bool Available { get; set; }

    // Joystick: -1..1 yatay eksen
    public static float MoveX { get; set; }

    // Buton durumları (her karede MobileControls tarafından set edilir)
    public static bool JumpPressed { get; set; }
    public static bool JumpHeld { get; set; }
    public static bool DashPressed { get; set; }
    public static bool AttackPressed { get; set; }
    public static bool PausePressed { get; set; }

    /// <summary>Diyalog / perk / menü ilerletme (ekranın herhangi bir yerine dokunma).</summary>
    public static bool TapAdvance { get; set; }

    /// <summary>Her karenin sonunda "pressed" bayraklarını sıfırlar.</summary>
    public static void LateReset()
    {
        JumpPressed = false;
        DashPressed = false;
        AttackPressed = false;
        PausePressed = false;
        TapAdvance = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        Available = false;
        MoveX = 0f;
        LateReset();
        JumpHeld = false;
    }
}
