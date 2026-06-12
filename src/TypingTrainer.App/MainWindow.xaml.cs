using Microsoft.UI.Xaml;
using TypingTrainer.App.Views;

namespace TypingTrainer.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        RootFrame.Navigate(typeof(PracticePage));
    }
}
