﻿namespace Wino.Core.Domain.Models.Reader
{
    /// <summary>
    /// Rendering options for mail.
    /// </summary>
    public class MailRenderingOptions
    {
        private const bool DefaultLoadImageValue = true;
        private const bool DefaultLoadStylesValue = true;

        public bool LoadImages { get; set; } = DefaultLoadImageValue;
        public bool LoadStyles { get; set; } = DefaultLoadStylesValue;

        // HtmlDocument.Load call is redundant if all the settings are in default values.
        // Therefore we will purify the HTML only if needed.

        public bool IsPurifyingNeeded() => LoadImages != DefaultLoadImageValue || LoadStyles != DefaultLoadStylesValue;
    }
}
