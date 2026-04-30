// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit.SliderGallery
{
    /// <summary>
    /// A popover that prompts the user to enter a folder name.
    /// Used for both creating and renaming folders.
    /// </summary>
    public partial class CreateFolderPopover : OsuPopover
    {
        public Action<string>? OnCommit;

        private readonly string title;
        private readonly string initialText;
        private OsuTextBox textBox = null!;

        public CreateFolderPopover(string title = "New folder", string initialText = "")
        {
            this.title = title;
            this.initialText = initialText;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Child = new FillFlowContainer
            {
                Width = 250,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 8),
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Text = title,
                        Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold),
                    },
                    textBox = new OsuTextBox
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 30,
                        Text = initialText,
                        PlaceholderText = "Folder name",
                        CommitOnFocusLost = true,
                    },
                }
            };

            textBox.OnCommit += (_, _) =>
            {
                string name = textBox.Text.Trim();

                if (!string.IsNullOrEmpty(name))
                    OnCommit?.Invoke(name);

                this.HidePopover();
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Schedule(() => GetContainingFocusManager()?.ChangeFocus(textBox));
        }
    }
}
