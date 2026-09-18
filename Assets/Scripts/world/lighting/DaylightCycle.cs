using System;
using UnityEngine;

namespace world.lighting
{
    /// <summary>Global daylight changes are constant-cost: voxel sky access never depends on time.</summary>
    public sealed class DaylightCycle : IDisposable
    {
        private static readonly int VoxelSkyStrength = Shader.PropertyToID("_VoxelSkyStrength");
        private static readonly int VoxelDarkness = Shader.PropertyToID("_VoxelDarkness");
        private static readonly int VoxelSkyColor = Shader.PropertyToID("_VoxelSkyColor");
        private static readonly int Exposure = Shader.PropertyToID("_Exposure");
        private static readonly int SkyTint = Shader.PropertyToID("_SkyTint");
        private static readonly int AtmosphereThickness = Shader.PropertyToID("_AtmosphereThickness");
        private static readonly int TwilightGlowColor = Shader.PropertyToID("_TwilightGlowColor");
        private static readonly int TwilightGlowStrength = Shader.PropertyToID("_TwilightGlowStrength");
        private static readonly int DaylightSunDirection = Shader.PropertyToID("_DaylightSunDirection");
        private static readonly int GroundColor = Shader.PropertyToID("_GroundColor");
        private const double DefaultDaySeconds = 1200;
        public const double MorningSeconds = 60;
        public double ElapsedSeconds { get; private set; }
        private double DaySeconds => DefaultDaySeconds;
        private float SkyStrength { get; set; }
        private const float DarknessFloor = .035f;
        private readonly Light _sun;
        private readonly Material _sky, _originalSky;
        private readonly Light _originalSun;
        private readonly Quaternion _originalRotation;
        private readonly float _originalIntensity;
        private readonly Color _originalColor;

        public DaylightCycle(double elapsedSeconds)
        {
            ElapsedSeconds = double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0
                ? MorningSeconds : elapsedSeconds;
            _originalSun = RenderSettings.sun;
            _sun = _originalSun;
            if (_sun == null)
                foreach (var light in UnityEngine.Object.FindObjectsByType<Light>())
                    if (light.type == LightType.Directional) { _sun = light; break; }
            if (_sun == null) throw new InvalidOperationException("Gameplay requires a directional sun light.");
            _originalRotation = _sun.transform.rotation;
            _originalIntensity = _sun.intensity;
            _originalColor = _sun.color;
            _originalSky = RenderSettings.skybox;
            Shader skyShader = Resources.Load<Shader>("VoxelSky");
            if (skyShader == null) throw new InvalidOperationException("VoxelSky shader is missing.");
            _sky = _originalSky != null ? new Material(_originalSky) : new Material(skyShader);
            _sky.shader = skyShader;
            _sky.EnableKeyword("_SUNDISK_HIGH_QUALITY");
            RenderSettings.skybox = _sky;
            RenderSettings.sun = _sun;
            Apply();
        }

        public void Advance(float activeSeconds)
        {
            ElapsedSeconds += Math.Max(0, activeSeconds);
            Apply();
        }

        public static float SolarElevation(double elapsed, double duration = DefaultDaySeconds) =>
            (float)Math.Sin(elapsed / Math.Max(1, duration) * Math.PI * 2);

        private void Apply()
        {
            float phase = (float)((ElapsedSeconds / Math.Max(1, DaySeconds)) % 1);
            float angle = phase * Mathf.PI * 2;
            Vector3 towardSun = new(Mathf.Cos(angle), Mathf.Sin(angle), 0);
            _sun.transform.rotation = Quaternion.LookRotation(-towardSun, Vector3.forward);
            float elevation = towardSun.y;
            float daylight = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elevation / .18f));
            float twilight = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(Mathf.Abs(elevation) / .16f));
            bool morning = phase < .25f || phase > .75f;
            Color warm = morning ? new Color(1f, .62f, .38f) : new Color(1f, .34f, .16f);
            _sun.color = Color.Lerp(Color.white, warm, twilight);
            _sun.intensity = 1.35f * daylight;
            SkyStrength = .34f * Mathf.SmoothStep(0, 1, Mathf.Clamp01((elevation + .12f) / .32f));
            Shader.SetGlobalFloat(VoxelSkyStrength, SkyStrength);
            Shader.SetGlobalFloat(VoxelDarkness, DarknessFloor);
            Shader.SetGlobalColor(VoxelSkyColor, Color.Lerp(new Color(.65f, .76f, 1f), warm, twilight * .4f));
            if (_sky != null)
            {
                _sky.SetFloat(Exposure, Mathf.Lerp(.025f, 1.25f, Mathf.Clamp01(SkyStrength / .34f)) + .22f * twilight);
                _sky.SetFloat(AtmosphereThickness, Mathf.Lerp(1f, 1.5f, twilight));
                _sky.SetColor(SkyTint, new Color(.5f, .5f, .5f));
                _sky.SetColor(TwilightGlowColor, warm);
                _sky.SetFloat(TwilightGlowStrength, twilight);
                _sky.SetVector(DaylightSunDirection, towardSun);
                _sky.SetColor(GroundColor, Color.Lerp(new Color(.015f, .02f, .03f), new Color(.32f, .3f, .28f), daylight));
            }
        }

        public float Brightness(Vector2 light) => Mathf.Clamp01(DarknessFloor + light.x * SkyStrength + light.y * light.y * .9f);

        public void Dispose()
        {
            RenderSettings.skybox = _originalSky;
            RenderSettings.sun = _originalSun;
            if (_sun != null)
            {
                _sun.transform.rotation = _originalRotation;
                _sun.intensity = _originalIntensity;
                _sun.color = _originalColor;
            }
            if (_sky != null) UnityEngine.Object.Destroy(_sky);
        }
    }
}
