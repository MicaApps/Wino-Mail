﻿using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.AppCenter.Crashes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using Wino.Core.Domain;
using Wino.Core.Domain.Enums;
using Wino.Core.Domain.Interfaces;
using Wino.Core.Messages.Mails;
using Wino.Core.Messages.Shell;
using Wino.Mail.ViewModels.Data;
using Wino.Views.Abstract;
using System.ComponentModel;
using Windows.UI.Xaml.Media;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using Windows.UI;
using Microsoft.Toolkit.Uwp;
using Wino.Extensions;
using Microsoft.Toolkit.Uwp.Helpers;

namespace Wino.Views
{
    public sealed partial class MailRenderingPage : MailRenderingPageAbstract,
        IRecipient<HtmlRenderingRequested>,
        IRecipient<CancelRenderingContentRequested>,
        IRecipient<NavigationPaneModeChanged>,
        IRecipient<ApplicationThemeChanged>,
        IRecipient<SaveAsPDFRequested>
    {
        private readonly IFontService _fontService = App.Current.Services.GetService<IFontService>();
        private readonly IDialogService _dialogService = App.Current.Services.GetService<IDialogService>();

        private bool isRenderingInProgress = false;
        private TaskCompletionSource<bool> DOMLoadedTask = new TaskCompletionSource<bool>();

        public WebView2 GetWebView() => Chromium;

        public MailRenderingPage()
        {
            InitializeComponent();

            Environment.SetEnvironmentVariable("WEBVIEW2_DEFAULT_BACKGROUND_COLOR", "00FFFFFF");
            Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS", "--enable-features=OverlayScrollbar,msOverlayScrollbarWinStyle,msOverlayScrollbarWinStyleAnimation");
            NavigationCacheMode = NavigationCacheMode.Enabled;
        }

        public override async void OnEditorThemeChanged()
        {
            base.OnEditorThemeChanged();

            await UpdateEditorThemeAsync();
        }

        private async Task<string> InvokeScriptSafeAsync(string function)
        {
            try
            {
                return await Chromium.ExecuteScriptAsync(function);
            }
            catch (Exception) { }

            return string.Empty;
        }

        public async Task<string> ExecuteScriptFunctionAsync(string functionName, params object[] parameters)
        {
            string script = functionName + "(";
            for (int i = 0; i < parameters.Length; i++)
            {
                script += JsonConvert.SerializeObject(parameters[i]);
                if (i < parameters.Length - 1)
                {
                    script += ", ";
                }
            }
            script += ");";

            return await Chromium.ExecuteScriptAsync(script);
        }

        string _htmlContent = "";

        private async Task RenderInternalAsync(string? htmlBody = null)
        {
            isRenderingInProgress = true;

            await DOMLoadedTask.Task;

            //await UpdateEditorThemeAsync();
            //await UpdateReaderFontPropertiesAsync();

            if (htmlBody != null)
            {
                _htmlContent = htmlBody;
            }

            Chromium.NavigateToString(ConvertContentTheme("<html><head></head><body>" + htmlBody + "</body></html>", ViewModel.IsDarkWebviewRenderer));
            
            //if (string.IsNullOrEmpty(htmlBody))
            //{
            //    await ExecuteScriptFunctionAsync("RenderHTML", " ");
            //}
            //else
            //{
            //    await ExecuteScriptFunctionAsync("RenderHTML", ConvertContentTheme("<html><head></head><body>" + htmlBody + "</body></html>", ViewModel.IsDarkWebviewRenderer));
            //}

            isRenderingInProgress = false;
        }

        private async void WindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
        {
            args.Handled = true;

            try
            {
                await Launcher.LaunchUriAsync(new Uri(args.Uri));
            }
            catch (Exception) { }
        }

        private void DOMContentLoaded(CoreWebView2 sender, CoreWebView2DOMContentLoadedEventArgs args) => DOMLoadedTask.TrySetResult(true);

        async void IRecipient<HtmlRenderingRequested>.Receive(HtmlRenderingRequested message)
        {
            if (message == null || string.IsNullOrEmpty(message.HtmlBody))
            {
                await RenderInternalAsync(string.Empty);
                return;
            }

            await Chromium.EnsureCoreWebView2Async();

            await RenderInternalAsync(message.HtmlBody);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            WeakReferenceMessenger.Default.Send(new CancelRenderingContentRequested());

            base.OnNavigatedFrom(e);
        }

        private string ConvertContentTheme(string original, bool isDarkMode, bool rawText = false)
        {
            if (isDarkMode)
            {
                if (rawText)
                {
                    return original;
                }
                else
                {
                    const string darkCss =
                        "<style>body{color: white;background: transparent !important; background-color: transparent !important;}</style>";
                    const string regexPattern =
                        """(bgcolor|background|color|background-color)\s*(\:|\=\")\s*(\S*)([\;\"])""";

                    var matches = Regex.Matches(original, regexPattern);
                    foreach (Match match in matches)
                    {
                        try
                        {
                            Color originalColor;
                            if (match.Groups[3].Value.StartsWith("rgba"))
                            {
                                var rgbaStr = match.Groups[3].Value.Substring(5).TrimEnd(')');
                                var rgbaArr = rgbaStr.Split(',');
                                originalColor = Color.FromArgb((byte)(double.Parse(rgbaArr[3]) * 255),
                                    Byte.Parse(rgbaArr[0]),
                                    Byte.Parse(rgbaArr[1]), Byte.Parse(rgbaArr[2]));
                            }
                            else if (match.Groups[3].Value.StartsWith("rgb"))
                            {
                                var rgbStr = match.Groups[3].Value.Substring(4).TrimEnd(')');
                                var rgbArr = rgbStr.Split(',');
                                originalColor = Color.FromArgb(255, Byte.Parse(rgbArr[0]),
                                    Byte.Parse(rgbArr[1]), Byte.Parse(rgbArr[2]));
                            }
                            else
                            {
                                originalColor = ColorExtensions.ParseColor(match.Groups[3].Value, null);
                            }


                            var hslColor = originalColor.ToHsl();

                            // 合理调整此处区间以便正常拉取色调
                            if (hslColor.L >= 0.70)
                            {
                                hslColor.L = 1 - hslColor.L;
                            }
                            else if (hslColor.L <= 0.30)
                            {
                                hslColor.L = 1 - hslColor.L;
                            }

                            var color = Microsoft.Toolkit.Uwp.Helpers.ColorHelper.FromHsl(
                                hslColor.H, hslColor.S, hslColor.L, hslColor.A);

                            if (match.Groups[1].Value.StartsWith('b') && hslColor.L is > 0.90 or < 0.10)
                            {
                                original = original.Replace(match.Value,
                                    match.Groups[1].Value + match.Groups[2].Value + "transparent" +
                                    match.Groups[4].Value);
                                continue;
                            }

                            original = original.Replace(match.Value,
                                match.Groups[1].Value + match.Groups[2].Value + $"#{color.R:X2}{color.G:X2}{color.B:X2}" +
                                match.Groups[4].Value);
                        }
                        catch
                        {
                            //ignore
                        }
                    }


                    if (original.Contains("<body>"))
                    {
                        original = original.Insert(original.IndexOf("<body>", StringComparison.Ordinal) + 6, darkCss);
                    }

                    if (original.Contains("<html>"))
                    {
                        original = original.Insert(original.IndexOf("<html>", StringComparison.Ordinal) + 6, darkCss);
                    }
                }

                // 处理表格
                original = Regex.Replace(original, @"border(-left|-right|-top|-bottom)*:(\S*)\s*windowtext",
                    "border$1:$2 white");
            }

            return original;
        }


        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var anim = ConnectedAnimationService.GetForCurrentView().GetAnimation("WebViewConnectedAnimation");
            anim?.TryStart(Chromium);

            Chromium.CoreWebView2Initialized -= CoreWebViewInitialized;
            Chromium.CoreWebView2Initialized += CoreWebViewInitialized;

            _ = Chromium.EnsureCoreWebView2Async();

            // We don't have shell initialized here. It's only standalone EML viewing.
            // Shift command bar from top to adjust the design.

            if (ViewModel.StatePersistanceService.ShouldShiftMailRenderingDesign)
                RendererGridFrame.Margin = new Thickness(0, 24, 0, 0);
            else
                RendererGridFrame.Margin = new Thickness(0, 0, 0, 0);
        }

        private async void CoreWebViewInitialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
        {
            if (Chromium.CoreWebView2 == null) return;

            var editorBundlePath = (await ViewModel.NativeAppService.GetQuillEditorBundlePathAsync()).Replace("full.html", string.Empty);

            Chromium.CoreWebView2.SetVirtualHostNameToFolderMapping("app.reader", editorBundlePath, CoreWebView2HostResourceAccessKind.Allow);

            Chromium.CoreWebView2.DOMContentLoaded -= DOMContentLoaded;
            Chromium.CoreWebView2.DOMContentLoaded += DOMContentLoaded;

            Chromium.CoreWebView2.NewWindowRequested -= WindowRequested;
            Chromium.CoreWebView2.NewWindowRequested += WindowRequested;

            Chromium.Source = new Uri("https://app.reader/reader.html");
        }


        async void IRecipient<CancelRenderingContentRequested>.Receive(CancelRenderingContentRequested message)
        {
            await Chromium.EnsureCoreWebView2Async();

            if (!isRenderingInProgress)
            {
                await RenderInternalAsync(string.Empty);
            }
        }

        void IRecipient<NavigationPaneModeChanged>.Receive(NavigationPaneModeChanged message)
        {
            if (message.NewMode == MenuPaneMode.Hidden)
                RendererBar.Margin = new Thickness(48, 6, 6, 6);
            else
                RendererBar.Margin = new Thickness(16, 6, 6, 6);
        }

        private async void WebViewNavigationStarting(WebView2 sender, CoreWebView2NavigationStartingEventArgs args)
        {
            // This is our reader.
            if (args.Uri == "https://app.reader/reader.html")
                return;

            // Cancel all external navigations since it's navigating to different address inside the WebView2.
            args.Cancel = !args.Uri.StartsWith("data:text/html");

            // TODO: Check external link navigation setting is enabled.
            // Open all external urls in launcher.

            if (args.Cancel && Uri.TryCreate(args.Uri, UriKind.Absolute, out Uri newUri))
            {
                await Launcher.LaunchUriAsync(newUri);
            }
        }

        private void AttachmentClicked(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is MailAttachmentViewModel attachmentViewModel)
            {
                ViewModel.OpenAttachmentCommand.Execute(attachmentViewModel);
            }
        }

        private void BarDynamicOverflowChanging(CommandBar sender, DynamicOverflowItemsChangingEventArgs args)
        {
            if (args.Action == CommandBarDynamicOverflowAction.AddingToOverflow || sender.SecondaryCommands.Any())
                sender.OverflowButtonVisibility = CommandBarOverflowButtonVisibility.Visible;
            else
                sender.OverflowButtonVisibility = CommandBarOverflowButtonVisibility.Collapsed;
        }

        private async Task UpdateEditorThemeAsync()
        {
            await DOMLoadedTask.Task;

            await InvokeScriptSafeAsync("ChangePrefferedTheme('light')");
            await InvokeScriptSafeAsync("DarkReader.disable();");

            if (ViewModel.IsDarkWebviewRenderer)
            {
                //Chromium.CoreWebView2.Profile.PreferredColorScheme = CoreWebView2PreferredColorScheme.Dark;

                //await InvokeScriptSafeAsync("ChangePrefferedTheme('dark')");
                //await InvokeScriptSafeAsync("DarkReader.enable();");
            }
            else
            {
                //Chromium.CoreWebView2.Profile.PreferredColorScheme = CoreWebView2PreferredColorScheme.Light;


                await InvokeScriptSafeAsync("SetLightEditor();");

            }
        }

        private async Task UpdateReaderFontPropertiesAsync()
        {
            await ExecuteScriptFunctionAsync("ChangeFontSize", _fontService.GetCurrentReaderFontSize());

            // Prepare font family name with fallback to sans-serif by default.
            var fontName = _fontService.GetCurrentReaderFont()?.FontFamilyName ?? "Arial";

            // If font family name is not supported by the browser, fallback to sans-serif.
            fontName += ", sans-serif";

            // var fontName = "Starborn";

            await ExecuteScriptFunctionAsync("ChangeFontFamily", fontName);
        }

        void IRecipient<ApplicationThemeChanged>.Receive(ApplicationThemeChanged message)
        {
            ViewModel.IsDarkWebviewRenderer = message.IsUnderlyingThemeDark;
        }

        public async void Receive(SaveAsPDFRequested message)
        {
            try
            {
                bool isSaved = await Chromium.CoreWebView2.PrintToPdfAsync(message.FileSavePath, null);

                if (isSaved)
                {
                    _dialogService.InfoBarMessage(Translator.Info_PDFSaveSuccessTitle,
                                                  string.Format(Translator.Info_PDFSaveSuccessMessage, message.FileSavePath),
                                                  InfoBarMessageType.Success);
                }
            }
            catch (Exception ex)
            {
                _dialogService.InfoBarMessage(Translator.Info_PDFSaveFailedTitle, ex.Message, InfoBarMessageType.Error);
                Crashes.TrackError(ex);
            }
        }
    }
}
