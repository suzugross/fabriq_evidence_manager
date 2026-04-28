using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace FabriqEvidenceManager.Helpers;

/// <summary>
/// PcEvidence.WindowsLicenseStatusDisplay / OfficeLicenseStatusDisplay の文字列値を
/// DataGrid セル背景色に変換する。Excel の MismatchBg / WarningBg / MatchBg / InfoBg と
/// 同系色を WPF Brush で再現。
/// </summary>
public sealed class LicenseStatusToBackgroundConverter : IValueConverter
{
    private static readonly Brush LicensedBrush     = new SolidColorBrush(Color.FromRgb(0xE2, 0xEF, 0xDA)); // 緑
    private static readonly Brush SignInPendingBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xF8, 0xE1)); // 黄
    private static readonly Brush AuthFailedBrush   = new SolidColorBrush(Color.FromRgb(0xFC, 0xE4, 0xEC)); // 赤
    private static readonly Brush InfoBrush          = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5)); // グレー

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string status) return Brushes.Transparent;

        return status switch
        {
            "認証済"            => LicensedBrush,
            "サインイン待ち"    => SignInPendingBrush,
            "認証失敗" or "未認証" => AuthFailedBrush,
            "未インストール" or "(未取得)" => InfoBrush,
            _                    => Brushes.Transparent,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
