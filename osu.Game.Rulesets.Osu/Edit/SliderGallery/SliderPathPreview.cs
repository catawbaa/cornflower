// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Skinning;
using osu.Game.Rulesets.Osu.Skinning.Argon;
using osu.Game.Rulesets.Osu.Skinning.Default;
using osu.Game.Rulesets.Osu.Skinning.Legacy;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Edit.SliderGallery
{
    /// <summary>
    /// A small drawable that renders a slider path using skin-appropriate visuals,
    /// with head and tail circles, auto-scaled to fit within a given bounding box.
    /// Detects the active skin (Legacy, Argon, or Default/Triangles) and renders
    /// the slider body and endpoint circles accordingly.
    /// </summary>
    /// <remarks>
    /// To ensure proper antialiasing at thumbnail scale, the path vertices and radius
    /// are pre-scaled to the final display size rather than rendering at full game
    /// resolution and using container scaling. This keeps <c>SmoothPath</c>'s edge AA
    /// operating at the correct pixel density.
    /// </remarks>
    public partial class SliderPathPreview : CompositeDrawable
    {
        private static readonly Color4 accent_colour = new Color4(0.35f, 0.75f, 0.4f, 1f);

        private readonly SliderGalleryEntry entry;

        // Raw (unscaled) data computed in load, applied at display scale in Update.
        private IReadOnlyList<Vector2>? rawCalculatedPath;
        private Vector2 rawTailPos;
        private float rawPathRadius;
        private PreviewSkinType skinType;

        private Container contentContainer = null!;
        private ManualSliderBody body = null!;
        private PreviewCirclePiece headCircle = null!;
        private PreviewCirclePiece? tailCircle;
        private bool layoutApplied;

        public SliderPathPreview(SliderGalleryEntry entry)
        {
            this.entry = entry;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            var controlPoints = entry.ControlPoints.Select(cp => cp.ToPathControlPoint()).ToArray();
            var sliderPath = new SliderPath(controlPoints, entry.ExpectedDistance);
            var calculatedPath = sliderPath.CalculatedPath;

            if (calculatedPath.Count == 0)
                return;

            skinType = detectSkinType(skin);
            rawCalculatedPath = calculatedPath.ToList();
            rawTailPos = sliderPath.PositionAt(1);

            body = createBody(skinType, skin, out rawPathRadius);
            // Set initial vertices so the body can auto-size for bounding box calculation.
            body.SetVertices(calculatedPath);

            headCircle = new PreviewCirclePiece(skinType, accent_colour, isHead: true);

            var children = new Drawable[] { body, headCircle };

            // Legacy skins show a visible tail circle (sliderendcircle).
            // Argon and Default skins don't render a tail circle.
            if (skinType == PreviewSkinType.Legacy)
            {
                tailCircle = new PreviewCirclePiece(skinType, accent_colour, isHead: false)
                {
                    Alpha = 0.8f,
                };
                children = new Drawable[] { body, tailCircle, headCircle };
            }

            InternalChild = contentContainer = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                AutoSizeAxes = Axes.Both,
                Children = children,
            };
        }

        /// <summary>
        /// Detects the active skin type by probing the skin source for known components.
        /// </summary>
        private static PreviewSkinType detectSkinType(ISkinSource skin)
        {
            // Try to create a slider body component — check its type without loading it.
            var testDrawable = skin.GetDrawableComponent(new OsuSkinComponentLookup(OsuSkinComponents.SliderBody));

            if (testDrawable is LegacySliderBody)
                return PreviewSkinType.Legacy;

            if (testDrawable is ArgonSliderBody)
                return PreviewSkinType.Argon;

            return PreviewSkinType.Default;
        }

        /// <summary>
        /// Creates the appropriate <see cref="ManualSliderBody"/> for the detected skin.
        /// The <paramref name="pathRadius"/> output is the unscaled path radius used for scale calculations.
        /// </summary>
        private static ManualSliderBody createBody(PreviewSkinType skinType, ISkinSource skin, out float pathRadius)
        {
            switch (skinType)
            {
                case PreviewSkinType.Legacy:
                {
                    pathRadius = OsuHitObject.OBJECT_RADIUS;
                    return new LegacyPreviewSliderBody
                    {
                        PathRadius = pathRadius,
                        AccentColour = (skin.GetConfig<OsuSkinColour, Color4>(OsuSkinColour.SliderTrackOverride)?.Value ?? accent_colour).Opacity(0.7f),
                        BorderColour = skin.GetConfig<OsuSkinColour, Color4>(OsuSkinColour.SliderBorder)?.Value ?? Color4.White,
                    };
                }

                case PreviewSkinType.Argon:
                {
                    pathRadius = ArgonMainCirclePiece.OUTER_GRADIENT_SIZE / 2;
                    float intendedThickness = ArgonMainCirclePiece.GRADIENT_THICKNESS / pathRadius;
                    float borderSize = intendedThickness / DrawableSliderPath.BORDER_PORTION;

                    return new ArgonPreviewSliderBody
                    {
                        PathRadius = pathRadius,
                        AccentColour = accent_colour,
                        BorderColour = accent_colour,
                        BorderSize = borderSize,
                    };
                }

                default:
                {
                    pathRadius = OsuHitObject.OBJECT_RADIUS;
                    return new ManualSliderBody
                    {
                        PathRadius = pathRadius,
                        AccentColour = accent_colour,
                    };
                }
            }
        }

        protected override void Update()
        {
            base.Update();

            if (layoutApplied || rawCalculatedPath == null || contentContainer == null || DrawWidth <= 0 || DrawHeight <= 0)
                return;

            // Compute the unscaled bounding box from the initial body layout.
            float contentWidth = contentContainer.DrawWidth;
            float contentHeight = contentContainer.DrawHeight;

            if (contentWidth <= 0 || contentHeight <= 0)
                return;

            float padding = 4;
            float availableWidth = DrawWidth - padding * 2;
            float availableHeight = DrawHeight - padding * 2;
            float scale = Math.Min(availableWidth / contentWidth, availableHeight / contentHeight);

            // Re-set the path at the final display scale so that SmoothPath's edge
            // antialiasing (which operates at a fixed pixel width) works correctly
            // at thumbnail size, rather than being rendered at full game resolution
            // and then crushed down via container scaling.
            body.PathRadius = rawPathRadius * scale;
            body.SetVertices(rawCalculatedPath.Select(v => v * scale).ToList());

            // Position circles relative to the body's (now-scaled) coordinate system.
            var pathOffset = body.PathOffset;
            headCircle.Position = pathOffset;
            headCircle.Scale = new Vector2(scale);

            if (tailCircle != null)
            {
                tailCircle.Position = pathOffset + rawTailPos * scale;
                tailCircle.Scale = new Vector2(scale);
            }

            layoutApplied = true;
        }
    }
}
