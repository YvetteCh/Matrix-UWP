﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Matrix_UWP.Helpers;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Matrix_UWP.UserControls {
  public sealed partial class NotificationList : UserControl, IHamburgerContent {
    internal ViewModel.NotificationViewModel vm = new ViewModel.NotificationViewModel();

    public event HamburgerContentHandler onError;

    public NotificationList() {
      InitializeComponent();
    }
    
    public async Task refreshList() {
      ObservableCollection<Model.Notification> newList = vm.list;
      vm.isLoading = true;
      try {
        newList = await Model.MatrixRequest.getNotificationList();
      } catch (MatrixException.SoftError err) {
        onError?.Invoke(this, new HamburgerContentEventArgs(err.Message));
        return;
      } catch (MatrixException.FatalError err) {
        onError?.Invoke(this, new HamburgerContentEventArgs(err.Message));
        return;
      } finally {
        vm.isLoading = false;
      }
      vm.updateWith(newList);
    }

    private async void checkbox_Checked(object sender, RoutedEventArgs e) {
      FrameworkElement elem = sender as FrameworkElement;
      Model.Notification notification = elem.DataContext as Model.Notification;
      if (notification == null) return;
      try {
        await notification.toggleReadState();
      } catch (MatrixException.MatrixException err) {
        onError?.Invoke(this, new HamburgerContentEventArgs(err.Message));
      }
    }

    public async Task ResetContentAsync() {
      await refreshList();
    }
  }
}
