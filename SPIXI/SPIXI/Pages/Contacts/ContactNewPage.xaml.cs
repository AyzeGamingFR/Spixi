﻿using IXICore;
using IXICore.Meta;
using SPIXI.Interfaces;
using SPIXI.Meta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using ZXing.Net.Mobile.Forms;

namespace SPIXI
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class ContactNewPage : SpixiContentPage
	{
        private string wallet_to_add = "";

		public ContactNewPage ()
		{
			InitializeComponent ();
            NavigationPage.SetHasNavigationBar(this, false);

            // Load the platform specific home page url
            var source = new UrlWebViewSource();
            source.Url = string.Format("{0}html/contact_new.html", DependencyService.Get<IBaseUrl>().Get());
            webView.Source = source;
        }

        public ContactNewPage(string wal_id)
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);

            wallet_to_add = wal_id;

            // Load the platform specific home page url
            var source = new UrlWebViewSource();
            source.Url = string.Format("{0}html/contact_new.html", DependencyService.Get<IBaseUrl>().Get());
            webView.Source = source;
        }

        private void onNavigated(object sender, WebNavigatedEventArgs e)
        {
            // Deprecated due to WPF, use onLoad
        }

        private void onLoad()
        {
            Utils.sendUiCommand(webView, "setAddress", wallet_to_add);
        }

        private void onNavigating(object sender, WebNavigatingEventArgs e)
        {
            string current_url = HttpUtility.UrlDecode(e.Url);

            if (current_url.Equals("ixian:onload", StringComparison.Ordinal))
            {
                onLoad();
            }
            else if (current_url.Equals("ixian:back", StringComparison.Ordinal))
            {
                Navigation.PopAsync(Config.defaultXamarinAnimations);
            }
            else if (current_url.Equals("ixian:error", StringComparison.Ordinal))
            {
                displaySpixiAlert("SPIXI Account", "Please type a wallet address.", "OK");
            }
            else if (current_url.Contains("ixian:request:"))
            {
                try
                {
                    string[] split = current_url.Split(new string[] { "ixian:request:" }, StringSplitOptions.None);
                    byte[] wal = Base58Check.Base58CheckEncoding.DecodePlain(split[1]);
                    onRequest(wal);
                }catch(Exception)
                {
                    displaySpixiAlert("Spixi", "Invalid address has been specified.", "OK");
                }
            }
            else if (current_url.Equals("ixian:quickscan", StringComparison.Ordinal))
            {
                ICustomQRScanner scanner = DependencyService.Get<ICustomQRScanner>();
                if (scanner != null && scanner.useCustomQRScanner())
                {
                    Logging.error("Custom scanner not implemented");
                    e.Cancel = true;
                    return;
                }
                quickScan();
            }
            else if (current_url.Contains("ixian:qrresult:"))
            {
                string[] split = current_url.Split(new string[] { "ixian:qrresult:" }, StringSplitOptions.None);
                string result = split[1];
                processQRResult(result);
                e.Cancel = true;
                return;
            }
            else
            {
                // Otherwise it's just normal navigation
                e.Cancel = false;
                return;
            }
            e.Cancel = true;

        }

        public async void quickScan()
        {
            ICustomQRScanner scanner = DependencyService.Get<ICustomQRScanner>();
            if (scanner != null && scanner.needsPermission())
            {
                if (!await scanner.requestPermission())
                {
                    return;
                }
            }

            var options = new ZXing.Mobile.MobileBarcodeScanningOptions();

            // Restrict to QR codes only
            options.PossibleFormats = new List<ZXing.BarcodeFormat>() {
                ZXing.BarcodeFormat.QR_CODE
            };

            var ScannerPage = new ZXingScannerPage(options);

            ScannerPage.OnScanResult += (result) => {

                ScannerPage.IsScanning = false;

                Device.BeginInvokeOnMainThread(() => {
                    Navigation.PopAsync(Config.defaultXamarinAnimations);

                    processQRResult(result.Text);
                });
            };


            await Navigation.PushAsync(ScannerPage, Config.defaultXamarinAnimations);
        }

        public void processQRResult(string result)
        {
            if (result.Contains(":ixi"))
            {
                string[] split = result.Split(new string[] { ":ixi" }, StringSplitOptions.None);
                if (split.Count() < 1)
                    return;
                string wal = split[0];
                Utils.sendUiCommand(webView, "setAddress", wal);

            }
            else
            {
                string wal = result;
                // TODO: enter exact Ixian address length
                if (wal.Length > 20 && wal.Length < 128)
                    Utils.sendUiCommand(webView, "setAddress", wal);
            }

        }

        public void onRequest(byte[] recipient_address)
        {
            try
            {
                if(Address.validateChecksum(recipient_address) == false)
                {
                    displaySpixiAlert("Invalid checksum", "Please make sure you typed the address correctly.", "OK");
                    return;
                }

                if(recipient_address.SequenceEqual(Node.walletStorage.getPrimaryAddress()))
                {
                    displaySpixiAlert("Cannot add yourself", "The address you have entered is your own address.", "OK");
                    return;
                }

                if (FriendList.getFriend(recipient_address) != null)
                {
                    displaySpixiAlert("Already exists", "This contact is already in your contacts list.", "OK");
                    return;
                }

                Friend friend = FriendList.addFriend(recipient_address, null, Base58Check.Base58CheckEncoding.EncodePlain(recipient_address), null, null, 0);

                FriendList.saveToStorage();

                StreamProcessor.sendContactRequest(friend);
            }catch(Exception)
            {

            }

            Navigation.PopAsync(Config.defaultXamarinAnimations);
        }

        protected override bool OnBackButtonPressed()
        {
            Navigation.PopAsync(Config.defaultXamarinAnimations);

            return true;
        }
    }
}