using UnityEngine;

namespace PrehistoricSurvival.Survival
{
    /// <summary>Creates lightweight camera-following weather particles when no authored systems are assigned.</summary>
    public class ParticleEffectsManager : MonoBehaviour
    {
        public int maxParticles = 700;
        public float spawnRadius = 14f;

        private void Update()
        {
            // Follow the camera on the ground plane: with the pitched 2.5D camera the
            // camera sits at z ≈ +5, so pinning the weather to z=0 keeps particles visible.
            if (Camera.main != null)
            {
                Vector3 p = Camera.main.transform.position;
                transform.position = new Vector3(p.x, p.y, 0f);
            }
        }

        private void Awake()
        {
            var weather = WeatherController.Instance != null ? WeatherController.Instance : GetComponent<WeatherController>();
            if (weather == null) return;
            if (weather.rainSystem == null) weather.rainSystem = CreateSystem("Rain", new Color(.55f,.7f,1f,.55f), 900, 18f, new Vector3(0,-1,0), .06f);
            if (weather.snowSystem == null) weather.snowSystem = CreateSystem("Snow", Color.white, maxParticles, 2f, new Vector3(0,-1,0), .14f);
            if (weather.stormSystem == null) weather.stormSystem = CreateSystem("Storm", new Color(.45f,.55f,.8f,.65f), maxParticles, 24f, new Vector3(0,-1,0), .08f);
            if (weather.fogSystem == null) weather.fogSystem = CreateSystem("Fog", new Color(.8f,.8f,.8f,.18f), 220, .3f, Vector3.zero, 3f);
        }

        private ParticleSystem CreateSystem(string name, Color color, int count, float speed, Vector3 direction, float size)
        {
            var go = new GameObject(name + "Particles"); go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main; main.loop = true; main.playOnAwake = false; main.maxParticles = count; main.startLifetime = 1.4f; main.startSpeed = speed; main.startSize = size; main.startColor = color; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission; emission.rateOverTime = count / 1.4f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(spawnRadius * 2f, 1f, 1f); shape.position = new Vector3(0, spawnRadius, 0);
            var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local; velocity.x = direction.x; velocity.y = direction.y;
            var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sortingOrder = 20;
            return ps;
        }
    }
}
