using UnityEngine;
using PrehistoricSurvival.Core;

namespace PrehistoricSurvival.Player
{
    /// <summary>Stamina-gated directional dodge with a short invulnerability window.</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class DodgeSystem : MonoBehaviour
    {
        public float dodgeSpeed = 10f, dodgeDuration = .18f, cooldown = .8f, staminaCost = 18f;
        public bool IsDodging { get; private set; }
        private Rigidbody2D _body; private Survival.SurvivalStats _stats; private float _cooldown;
        private void Awake() { _body = GetComponent<Rigidbody2D>(); _stats = GetComponent<Survival.SurvivalStats>(); }
        private void Update()
        {
            _cooldown -= Time.deltaTime;
            if ((_cooldown <= 0f && Input.GetKeyDown(KeyCode.LeftControl)) || (_cooldown <= 0f && Input.GetKeyDown(KeyCode.J))) TryDodge();
        }
        public bool TryDodge()
        {
            if (IsDodging || _cooldown > 0f || (_stats != null && _stats.Stamina < staminaCost)) return false;
            if (_stats != null) _stats.Stamina -= staminaCost;
            Vector2 direction = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")); if (direction.sqrMagnitude < .01f) direction = Vector2.right;
            StartCoroutine(Dodge(direction.normalized)); return true;
        }
        private System.Collections.IEnumerator Dodge(Vector2 direction)
        {
            IsDodging = true; _cooldown = cooldown; float elapsed = 0f;
            while (elapsed < dodgeDuration) { elapsed += Time.deltaTime; _body.linearVelocity = direction * dodgeSpeed; yield return new WaitForFixedUpdate(); }
            IsDodging = false;
        }
    }
}
