// WinForms is referenced for NotifyIcon only, but that makes ImplicitUsings pull in
// System.Drawing and System.Windows.Forms alongside WPF, so Brush/Color/Application all
// become ambiguous. These aliases pin every one of them to the WPF type; the handful of
// WinForms types actually used are written out fully qualified at the call site.

global using Application = System.Windows.Application;
global using Brush = System.Windows.Media.Brush;
global using Brushes = System.Windows.Media.Brushes;
global using Color = System.Windows.Media.Color;
global using FontFamily = System.Windows.Media.FontFamily;
global using Point = System.Windows.Point;
global using Size = System.Windows.Size;
