﻿using IXICore;
using IXICore.Meta;
using SPIXI.Interfaces;
using SPIXI.Meta;
using System;
using System.Linq;
using System.Text;
using System.Web;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace SPIXI
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class WalletReceivePage : SpixiContentPage
	{
        private Friend local_friend = null;

		public WalletReceivePage ()
		{
			InitializeComponent ();
//            pubkeyAddress.Text = Node.walletStorage.address;
            NavigationPage.SetHasNavigationBar(this, false);


            loadPage(webView, "wallet_request.html");
        }

        public WalletReceivePage(Friend friend)
        {
            InitializeComponent();
            //            pubkeyAddress.Text = Node.walletStorage.address;
            NavigationPage.SetHasNavigationBar(this, false);

            local_friend = friend;

            loadPage(webView, "wallet_request.html");
        }

        private void onNavigated(object sender, WebNavigatedEventArgs e)
        {
            // Deprecated due to WPF, use onLoad
        }

        private void onLoad()
        {
            Utils.sendUiCommand(webView, "setAddress", Base58Check.Base58CheckEncoding.EncodePlain(Node.walletStorage.getPrimaryAddress()));

            // Check if this page is accessed from the home wallet
            if (local_friend == null)
            {
                //webView.Eval("hideRequest()");
            }
            else
            {
                Utils.sendUiCommand(webView, "addRecipient", local_friend.nickname, Base58Check.Base58CheckEncoding.EncodePlain(local_friend.walletAddress));
            }
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
            else if (current_url.Equals("ixian:pick", StringComparison.Ordinal))
            {
                var recipientPage = new WalletRecipientPage();
                recipientPage.pickSucceeded += HandlePickSucceeded;
                Navigation.PushModalAsync(recipientPage);
            }
            else if (current_url.Equals("ixian:error", StringComparison.Ordinal))
            {
                displaySpixiAlert("SPIXI", "Please type an amount.", "OK");
            }
            else if (current_url.Contains("ixian:sendrequest:"))
            {
                try
                {
                    string[] split = current_url.Split(new string[] { "ixian:sendrequest:" }, StringSplitOptions.None);

                    // Extract all addresses and amounts
                    string[] addresses_split = split[1].Split(new string[] { "|" }, StringSplitOptions.None);

                    foreach (var address_amount in addresses_split)
                    {
                        if(address_amount == "")
                        {
                            continue;
                        }

                        string[] split_address_amount = address_amount.Split(':');
                        if (split_address_amount.Count() < 2)
                            continue;

                        string recipient = split_address_amount[0];
                        string amount = split_address_amount[1];
                        if (Address.validateChecksum(Base58Check.Base58CheckEncoding.DecodePlain(recipient)) == false)
                        {
                            e.Cancel = true;
                            displaySpixiAlert("Invalid address checksum", "Please make sure you typed the address correctly.", "OK");
                            return;
                        }
                        string[] amount_split = amount.Split(new string[] { "." }, StringSplitOptions.None);
                        if (amount_split.Length > 2)
                        {
                            displaySpixiAlert("SPIXI", "Please type a correct decimal amount.", "OK");
                            e.Cancel = true;
                            return;
                        }

                        // Add decimals if none found
                        if (amount_split.Length == 1)
                            amount = String.Format("{0}.0", amount);

                        IxiNumber _amount = amount;

                        if (_amount == 0)
                        {
                            displaySpixiAlert("SPIXI", "Incorrect amount '" + amount + "' was specified.", "OK");
                            e.Cancel = true;
                            return;
                        }

                        if (_amount < (long)0)
                        {
                            displaySpixiAlert("SPIXI", "Please type a positive amount.", "OK");
                            e.Cancel = true;
                            return;
                        }
                        onRequest(recipient, amount);
                    }
                }catch(Exception ex)
                {
                    Logging.error("Exception occurent for sendrequest action: " + ex);
                    displaySpixiAlert("Spixi", "Invalid data has been specified.", "OK");
                }
            }
            else if (current_url.Contains("ixian:addrecipient"))
            {
                try
                {
                    string[] split = current_url.Split(new string[] { "ixian:addrecipient:" }, StringSplitOptions.None);
                    if (Address.validateChecksum(Base58Check.Base58CheckEncoding.DecodePlain(split[1])))
                    {
                        Utils.sendUiCommand(webView, "addRecipient", split[1], split[1]);
                    }
                    else
                    {
                        displaySpixiAlert("Spixi", "Invalid address has been specified.", "OK");
                    }
                }
                catch (Exception)
                {
                    displaySpixiAlert("Spixi", "Invalid address has been specified.", "OK");
                }
            }
            else
            {
                // Otherwise it's just normal navigation
                e.Cancel = false;
                return;
            }
            e.Cancel = true;

        }

        private void onRequest(string recipient, string amount)
        {
            byte[] recipient_bytes = Base58Check.Base58CheckEncoding.DecodePlain(recipient);
            Friend friend = FriendList.getFriend(recipient_bytes);
            if (friend != null && (new IxiNumber(amount)) > 0)
            {
                FriendMessage friend_message = FriendList.addMessageWithType(null, FriendMessageType.requestFunds, friend.walletAddress, amount, true);

                SpixiMessage spixi_message = new SpixiMessage(SpixiMessageCode.requestFunds, Encoding.UTF8.GetBytes(amount));

                StreamMessage message = new StreamMessage();
                message.type = StreamMessageCode.info;
                message.recipient = Base58Check.Base58CheckEncoding.DecodePlain(recipient);
                message.sender = Node.walletStorage.getPrimaryAddress();
                message.transaction = new byte[1];
                message.sigdata = new byte[1];
                message.data = spixi_message.getBytes();
                message.id = friend_message.id;

                StreamProcessor.sendMessage(friend, message);

                Navigation.PopAsync(Config.defaultXamarinAnimations);
            }// else error?

        }

        private void HandlePickSucceeded(object sender, SPIXI.EventArgs<string> e)
        {
            string wallets_to_send = e.Value;

            string[] wallet_arr = wallets_to_send.Split('|');

            foreach (string wallet_to_send in wallet_arr)
            {
                Friend friend = FriendList.getFriend(Base58Check.Base58CheckEncoding.DecodePlain(wallet_to_send));

                string nickname = wallet_to_send;
                if (friend != null)
                    nickname = friend.nickname;

                Utils.sendUiCommand(webView, "addRecipient", nickname, wallet_to_send);
            }
            Navigation.PopModalAsync();
        }

        protected override bool OnBackButtonPressed()
        {
            Navigation.PopAsync(Config.defaultXamarinAnimations);

            return true;
        }
    }
}