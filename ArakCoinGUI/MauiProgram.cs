using Microsoft.AspNetCore.Components.WebView.Maui;
using ArakCoin_GUI.Data;

namespace ArakCoin_GUI;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();
		#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		#endif

		//begin main program logic from ArakCoin class library
		Startup.Begin();

		return builder.Build();
	}
}
