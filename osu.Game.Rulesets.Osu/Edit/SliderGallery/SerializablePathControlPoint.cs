// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Newtonsoft.Json;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit.SliderGallery
{
    /// <summary>
    /// A JSON-serializable representation of a <see cref="PathControlPoint"/>.
    /// </summary>
    public class SerializablePathControlPoint
    {
        [JsonProperty("position")]
        public Vector2 Position { get; set; }

        [JsonProperty("type")]
        public PathType? Type { get; set; }

        public SerializablePathControlPoint()
        {
        }

        public SerializablePathControlPoint(PathControlPoint controlPoint)
        {
            Position = controlPoint.Position;
            Type = controlPoint.Type;
        }

        public PathControlPoint ToPathControlPoint() => new PathControlPoint(Position, Type);
    }
}
