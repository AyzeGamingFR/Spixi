﻿using Android.App;
using Android.Content.PM;
using Android.OS;
using System.Threading.Tasks;
using System.IO;
using Android.Content;
using Plugin.LocalNotifications;
using Xamarin.Forms;
using SPIXI.Interfaces;
using Android.Views;
using SPIXI.Droid.Classes;
using Android.Systems;
using Android.Content.Res;

namespace SPIXI.Droid
{
    [Activity(Label = "SPIXI", Icon = "@mipmap/ic_launcher", RoundIcon = "@mipmap/ic_round_launcher", Theme = "@style/MainTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation, LaunchMode = LaunchMode.SingleInstance)]
    public class MainActivity : global::Xamarin.Forms.Platform.Android.FormsAppCompatActivity
    {
        private ProximitySensor proximitySensor = null;

        // Field, property, and method for Picture Picker
        public static readonly int PickImageId = 1000;

        public TaskCompletionSource<SpixiImageData> PickImageTaskCompletionSource { set; get; }

        internal static MainActivity Instance { get; private set; }

        protected override void OnCreate(Bundle bundle)
        {
            Instance = this;

            TabLayoutResource = Resource.Layout.Tabbar;
            ToolbarResource = Resource.Layout.Toolbar;
            
            base.OnCreate(bundle);

            global::Xamarin.Forms.Forms.Init(this, bundle);
            
            ZXing.Net.Mobile.Forms.Android.Platform.Init();
            ZXing.Mobile.MobileBarcodeScanner.Initialize(this.Application);

            string fa = Intent.GetStringExtra("fa");
            if (fa != null)
            {
                Intent.RemoveExtra("fa");
                App.startingScreen = fa;
            }else
            {
                App.startingScreen = "";
            }
            
            // Initialize Push Notification service
            DependencyService.Get<IPushService>().initialize();

            // CLear notifications
            DependencyService.Get<IPushService>().clearNotifications();

            proximitySensor = new ProximitySensor();

            IXICore.CryptoManager.initLib(new CryptoLibs.BouncyCastleAndroid());
            LoadApplication(App.Instance());

            this.Window.ClearFlags(WindowManagerFlags.Fullscreen);

            LocalNotificationsImplementation.NotificationIconId = Resource.Drawable.statusicon;
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent intent)
        {
            base.OnActivityResult(requestCode, resultCode, intent);

            if (requestCode == PickImageId)
            {
                if ((resultCode == Result.Ok) && (intent != null))
                {
                    Android.Net.Uri uri = intent.Data;

                    SpixiImageData spixi_img_data = new SpixiImageData() { name = Path.GetFileName(uri.Path), path = uri.Path, stream = ContentResolver.OpenInputStream(uri)};

                    // Set the Stream as the completion of the Task
                    PickImageTaskCompletionSource.SetResult(spixi_img_data);
                }
                else
                {
                    PickImageTaskCompletionSource.SetResult(null);
                }
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            if (permissions[0] == "android.permission.CAMERA")
            {
                // prevent ZXing related crash on denied
                if (grantResults[0] == Permission.Denied)
                {
                    Xamarin.Forms.Application.Current.MainPage.Navigation.PopAsync(SPIXI.Meta.Config.defaultXamarinAnimations);
                    Xamarin.Forms.Application.Current.MainPage.DisplayAlert("Permission error", "Camera access must be allowed to use this feature.", "OK");
                    return;
                }
                ZXing.Net.Mobile.Android.PermissionsHandler.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            }else if(permissions[0] == "android.permission.RECORD_AUDIO")
            {
                if (grantResults[0] == Permission.Denied)
                {
                    // TODO TODO do something here
                }
            }
        }

        protected override void OnNewIntent(Intent intent)
        {
            base.OnNewIntent(intent);

            // Handle local notification tap
            string fa = intent.GetStringExtra("fa");
            if (fa != null)
            {
                HomePage.Instance().onChat(fa, null);
            }
        }

        protected override void OnPause()
        {
            base.OnPause();
            if (proximitySensor != null)
            {
                proximitySensor.OnPause();
            }
        }

        protected override void OnResume()
        {
            base.OnResume();
            if (proximitySensor != null)
            {
                proximitySensor.OnResume();
            }
        }
    }
}

