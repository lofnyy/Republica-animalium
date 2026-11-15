using UnityEngine;

public class ControlsHint : MonoBehaviour
{
    public Health target;

    bool _started;

    void Awake()
    {
        if (target != null) return;
        Debug.LogError("ControlsHint needs a target Health reference.", this);
        enabled = false;
    }

    void OnEnable()
    {
        if (target == null) return;
        target.Died += Hide;
        if (_started && target.IsDead) Hide();
    }

    void Start()
    {
        _started = true;
        if (target != null && target.IsDead) Hide();
    }

    void OnDisable()
    {
        if (target != null) target.Died -= Hide;
    }

    void Hide()
    {
        gameObject.SetActive(false);
    }
}
