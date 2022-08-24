using System.Threading.Tasks;
using Grpc.Net.Client;
using System;
using System.Windows;
using Google.Protobuf.WellKnownTypes;
using System.Windows.Media;
using System.Windows.Controls;
using System.Threading;
using System.Collections.Generic;

namespace GrpcClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ClientViewModel _model;
        public MainWindow()
        {
            InitializeComponent();
            _model = (ClientViewModel)DataContext;
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            _model.Start();
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
           _model.Stop();
        }
    }
}
