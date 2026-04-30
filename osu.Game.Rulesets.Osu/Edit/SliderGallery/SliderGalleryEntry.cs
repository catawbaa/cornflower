// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace osu.Game.Rulesets.Osu.Edit.SliderGallery
{
    /// <summary>
    /// Represents a single saved slider shape in the slider gallery.
    /// </summary>
    public class SliderGalleryEntry
    {
        [JsonProperty("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("control_points")]
        public List<SerializablePathControlPoint> ControlPoints { get; set; } = new List<SerializablePathControlPoint>();

        [JsonProperty("expected_distance")]
        public double? ExpectedDistance { get; set; }

        [JsonProperty("slider_velocity_multiplier")]
        public double SliderVelocityMultiplier { get; set; } = 1;

        [JsonProperty("repeat_count")]
        public int RepeatCount { get; set; }

        [JsonProperty("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [JsonProperty("folder_id")]
        public Guid? FolderId { get; set; }
    }
}
