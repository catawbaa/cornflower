// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;

namespace osu.Game.Screens.Edit
{
    internal class PlayfieldZoomMenuItem : MenuItem
    {
        private readonly Bindable<float> playfieldZoom;

        private readonly Dictionary<float, TernaryStateRadioMenuItem> menuItemLookup = new Dictionary<float, TernaryStateRadioMenuItem>();

        public PlayfieldZoomMenuItem(Bindable<float> playfieldZoom)
            : base(EditorStrings.PlayfieldZoom)
        {
            Items = new[]
            {
                createMenuItem(0.25f),
                createMenuItem(0.5f),
                createMenuItem(0.75f),
                createMenuItem(1f),
            };

            this.playfieldZoom = playfieldZoom;
            playfieldZoom.BindValueChanged(zoom =>
            {
                foreach (var kvp in menuItemLookup)
                    kvp.Value.State.Value = kvp.Key == zoom.NewValue ? TernaryState.True : TernaryState.False;
            }, true);
        }

        private TernaryStateRadioMenuItem createMenuItem(float zoom)
        {
            var item = new TernaryStateRadioMenuItem($"{zoom * 100}%", MenuItemType.Standard, _ => updateZoom(zoom));
            menuItemLookup[zoom] = item;
            return item;
        }

        private void updateZoom(float zoom) => playfieldZoom.Value = zoom;
    }
}
