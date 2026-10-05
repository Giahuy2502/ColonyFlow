using System;
using UnityEngine;
using UnityEngine.UI;

namespace ColonyFlow
{
    [DisallowMultipleComponent]
    public sealed class VictoryStarFall : MonoBehaviour
    {
        [Header("Pool")]
        [SerializeField] private GameUnit starPrefab;
        [SerializeField, Min(0)] private int starCount = 24;

        [Header("Size Distribution")]
        [SerializeField] private Vector2 smallSize = new Vector2(22f, 36f);
        [SerializeField] private Vector2 mediumSize = new Vector2(38f, 52f);
        [SerializeField] private Vector2 largeSize = new Vector2(58f, 70f);
        [SerializeField, Range(0f, 1f)] private float smallWeight = 0.7f;
        [SerializeField, Range(0f, 1f)] private float mediumWeight = 0.25f;
        [SerializeField] private Vector2 largeSpeedMultiplier = new Vector2(0.78f, 0.9f);

        [Header("Falling")]
        [SerializeField] private Vector2 fallSpeed = new Vector2(210f, 340f);
        [SerializeField] private Vector2 verticalAcceleration = new Vector2(-8f, 14f);
        [SerializeField] private Vector2 speedVariation = new Vector2(0.08f, 0.18f);
        [SerializeField] private Vector2 speedVariationFrequency = new Vector2(0.6f, 1.2f);

        [Header("Horizontal Sway")]
        [SerializeField] private Vector2 horizontalDrift = new Vector2(-24f, 24f);
        [SerializeField] private Vector2 primarySwayAmplitude = new Vector2(25f, 80f);
        [SerializeField] private Vector2 primarySwayFrequency = new Vector2(1.1f, 2.1f);
        [SerializeField] private Vector2 secondarySwayAmplitude = new Vector2(4f, 16f);
        [SerializeField] private Vector2 secondarySwayFrequency = new Vector2(2.5f, 4.8f);

        [Header("Rotation And Flip")]
        [SerializeField] private Vector2 angularVelocity = new Vector2(-120f, 120f);
        [SerializeField] private Vector2 flipSpeed = new Vector2(1.4f, 3.2f);
        [SerializeField, Range(0.01f, 1f)] private float minimumEdgeScale = 0.15f;
        [SerializeField, Range(0f, 1f)] private float signedFlipChance = 0.3f;
        [SerializeField] private Vector2 scaleY = new Vector2(0.85f, 1f);
        [SerializeField] private Vector2 scaleYSpeed = new Vector2(0.7f, 1.6f);

        [Header("Spawn And Fade")]
        [SerializeField] private Vector2 spawnBand = new Vector2(40f, 180f);
        [SerializeField, Min(0.01f)] private float fadeInDuration = 0.25f;
        [SerializeField, Min(1f)] private float fadeOutDistance = 180f;
        [SerializeField] private Color[] palette =
        {
            new Color32(0x62, 0xEA, 0xF4, 0xFF),
            new Color32(0xFF, 0xFF, 0xFF, 0xFF),
            new Color32(0xFF, 0xD8, 0x4A, 0xFF),
            new Color32(0xFF, 0xB9, 0x28, 0xFF)
        };

        private RectTransform rootRect;
        private StarRuntime[] stars;
        private System.Random random;
        private int activeCount;
        private bool poolPrepared;
        private bool running;

        private struct StarRuntime
        {
            public GameUnit Unit;
            public RectTransform Rect;
            public Image Image;
            public Color Color;
            public float Size;
            public float StartX;
            public float Y;
            public float Age;
            public float BaseSpeed;
            public float Acceleration;
            public float SpeedVariation;
            public float SpeedFrequency;
            public float SpeedPhase;
            public float Drift;
            public float PrimaryAmplitude;
            public float PrimaryFrequency;
            public float PrimaryPhase;
            public float SecondaryAmplitude;
            public float SecondaryFrequency;
            public float SecondaryPhase;
            public float Angle;
            public float AngularVelocity;
            public float FlipSpeed;
            public float FlipPhase;
            public float ScaleYSpeed;
            public float ScaleYPhase;
            public bool SignedFlip;
        }

        private void Awake()
        {
            rootRect = transform as RectTransform;
            random = new System.Random(unchecked(Environment.TickCount * 31 + GetInstanceID()));
            EnsurePool();
            enabled = false;
        }

        private void OnDisable()
        {
            if (running)
                StopAndClear();
        }

        private void Update()
        {
            if (!running || rootRect == null)
                return;

            float deltaTime = Time.unscaledDeltaTime;
            if (deltaTime <= 0f)
                return;

            Rect bounds = rootRect.rect;
            float bottom = bounds.yMin;

            for (int i = 0; i < activeCount; i++)
            {
                ref StarRuntime star = ref stars[i];
                star.Age += deltaTime;

                float speedModulation = 1f +
                    Mathf.Sin(star.Age * star.SpeedFrequency + star.SpeedPhase) * star.SpeedVariation;
                float currentSpeed = Mathf.Max(40f,
                    (star.BaseSpeed + star.Acceleration * star.Age) * speedModulation);
                star.Y -= currentSpeed * deltaTime;

                if (star.Y < bottom - star.Size * 0.5f)
                    ResetStar(ref star, false);

                float x = star.StartX + star.Drift * star.Age +
                    Mathf.Sin(star.Age * star.PrimaryFrequency + star.PrimaryPhase) * star.PrimaryAmplitude +
                    Mathf.Sin(star.Age * star.SecondaryFrequency + star.SecondaryPhase) * star.SecondaryAmplitude;

                star.Angle = Mathf.Repeat(star.Angle + star.AngularVelocity * deltaTime, 360f);

                float flip = Mathf.Cos(star.Age * star.FlipSpeed + star.FlipPhase);
                float scaleX;
                if (star.SignedFlip)
                {
                    float sign = flip < 0f ? -1f : 1f;
                    scaleX = sign * Mathf.Max(minimumEdgeScale, Mathf.Abs(flip));
                }
                else
                {
                    scaleX = Mathf.Lerp(minimumEdgeScale, 1f, Mathf.Abs(flip));
                }

                float scaleYValue = Mathf.Lerp(
                    Min(scaleY), Max(scaleY),
                    Mathf.Sin(star.Age * star.ScaleYSpeed + star.ScaleYPhase) * 0.5f + 0.5f);

                float fadeIn = Mathf.SmoothStep(0f, 1f, star.Age / fadeInDuration);
                float fadeOut = Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01((star.Y - bottom) / fadeOutDistance));
                Color color = star.Color;
                color.a *= Mathf.Min(fadeIn, fadeOut);

                star.Rect.anchoredPosition = new Vector2(x, star.Y);
                star.Rect.localRotation = Quaternion.Euler(0f, 0f, star.Angle);
                star.Rect.localScale = new Vector3(scaleX, scaleYValue, 1f);
                star.Image.color = color;
            }
        }

        public void Play()
        {
            StopAndClear();
            EnsurePool();

            if (!poolPrepared || starCount <= 0 || rootRect == null)
                return;

            if (stars == null || stars.Length != starCount)
                stars = new StarRuntime[starCount];

            activeCount = 0;
            for (int i = 0; i < starCount; i++)
            {
                GameUnit unit = SimplePool.Spawn<GameUnit>(
                    PoolType.VictoryStar, Vector3.zero, Quaternion.identity);
                if (unit == null)
                    break;

                RectTransform rect = unit.transform as RectTransform;
                Image image = unit.GetComponent<Image>();
                if (rect == null || image == null)
                {
                    SimplePool.Despawn(unit);
                    continue;
                }

                rect.SetParent(rootRect, false);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                image.raycastTarget = false;

                ref StarRuntime star = ref stars[activeCount++];
                star.Unit = unit;
                star.Rect = rect;
                star.Image = image;
                ResetStar(ref star, true);
            }

            running = activeCount > 0;
            enabled = running;
        }

        public void StopAndClear()
        {
            running = false;

            for (int i = 0; i < activeCount; i++)
            {
                GameUnit unit = stars[i].Unit;
                if (unit != null && unit.gameObject.activeSelf)
                    SimplePool.Despawn(unit);
                stars[i].Unit = null;
                stars[i].Rect = null;
                stars[i].Image = null;
            }

            activeCount = 0;
            enabled = false;
        }

        private void EnsurePool()
        {
            if (poolPrepared || starPrefab == null || starCount <= 0)
                return;

            SimplePool.PreLoad(starPrefab, starCount, transform);
            poolPrepared = true;
        }

        private void ResetStar(ref StarRuntime star, bool distributeVertically)
        {
            Rect bounds = rootRect.rect;
            bool isLarge;
            float sizeRoll = NextFloat();
            float size;
            if (sizeRoll < smallWeight)
            {
                size = RandomRange(smallSize);
                isLarge = false;
            }
            else if (sizeRoll < smallWeight + mediumWeight)
            {
                size = RandomRange(mediumSize);
                isLarge = false;
            }
            else
            {
                size = RandomRange(largeSize);
                isLarge = true;
            }

            star.Size = size;
            star.Rect.sizeDelta = new Vector2(size, size);
            star.Rect.localScale = Vector3.one;

            star.BaseSpeed = RandomRange(fallSpeed);
            if (isLarge)
                star.BaseSpeed *= RandomRange(largeSpeedMultiplier);
            star.Acceleration = RandomRange(verticalAcceleration);
            star.SpeedVariation = RandomRange(speedVariation);
            star.SpeedFrequency = RandomRange(speedVariationFrequency);
            star.SpeedPhase = RandomAngle();
            star.Drift = RandomRange(horizontalDrift);
            star.PrimaryAmplitude = RandomRange(primarySwayAmplitude);
            star.PrimaryFrequency = RandomRange(primarySwayFrequency);
            star.PrimaryPhase = RandomAngle();
            star.SecondaryAmplitude = RandomRange(secondarySwayAmplitude);
            star.SecondaryFrequency = RandomRange(secondarySwayFrequency);
            star.SecondaryPhase = RandomAngle();
            star.Angle = RandomRange(0f, 360f);
            star.AngularVelocity = RandomRange(angularVelocity);
            star.FlipSpeed = RandomRange(flipSpeed);
            star.FlipPhase = RandomAngle();
            star.ScaleYSpeed = RandomRange(scaleYSpeed);
            star.ScaleYPhase = RandomAngle();
            star.SignedFlip = NextFloat() < signedFlipChance;

            float estimatedTravelTime = bounds.height / Mathf.Max(40f, star.BaseSpeed);
            float horizontalReserve = size * 0.5f + star.PrimaryAmplitude + star.SecondaryAmplitude +
                Mathf.Abs(star.Drift) * estimatedTravelTime;
            float horizontalLimit = Mathf.Max(0f, bounds.width * 0.5f - horizontalReserve);
            star.StartX = RandomRange(-horizontalLimit, horizontalLimit);

            if (distributeVertically)
            {
                star.Y = RandomRange(
                    bounds.yMin + size * 0.5f,
                    bounds.yMax + Max(spawnBand) + size * 0.5f);
                star.Age = RandomRange(0f, 3f);
            }
            else
            {
                star.Y = bounds.yMax + RandomRange(spawnBand) + size * 0.5f;
                star.Age = 0f;
            }

            star.Color = GetRandomColor();
            star.Image.color = star.Color;
            star.Rect.anchoredPosition = new Vector2(star.StartX, star.Y);
            star.Rect.localRotation = Quaternion.Euler(0f, 0f, star.Angle);
        }

        private Color GetRandomColor()
        {
            if (palette == null || palette.Length == 0)
                return Color.white;
            return palette[random.Next(0, palette.Length)];
        }

        private float RandomRange(Vector2 range)
        {
            return RandomRange(Min(range), Max(range));
        }

        private float RandomRange(float minimum, float maximum)
        {
            return minimum + (maximum - minimum) * NextFloat();
        }

        private float NextFloat()
        {
            return (float)random.NextDouble();
        }

        private float RandomAngle()
        {
            return NextFloat() * Mathf.PI * 2f;
        }

        private static float Min(Vector2 value)
        {
            return Mathf.Min(value.x, value.y);
        }

        private static float Max(Vector2 value)
        {
            return Mathf.Max(value.x, value.y);
        }

        private void OnValidate()
        {
            starCount = Mathf.Max(0, starCount);
            smallWeight = Mathf.Clamp01(smallWeight);
            mediumWeight = Mathf.Clamp(mediumWeight, 0f, 1f - smallWeight);
            minimumEdgeScale = Mathf.Clamp(minimumEdgeScale, 0.01f, 1f);
            signedFlipChance = Mathf.Clamp01(signedFlipChance);
            fadeInDuration = Mathf.Max(0.01f, fadeInDuration);
            fadeOutDistance = Mathf.Max(1f, fadeOutDistance);
        }
    }
}
