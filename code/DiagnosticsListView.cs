using Editor;
using Sandbox.Internal;

namespace Gooman.Tools.ProjectDiagnostics;

/// <summary>
/// A list that can contain compiler diagnostics.
/// </summary>
public sealed class DiagnosticsListView : ListView
{
	/// <summary>
	/// Initializes a new instance of <see cref="DiagnosticsListView"/>.
	/// </summary>
	/// <param name="parent">The parent that is containing this element.</param>
	public DiagnosticsListView( Widget parent ) : base( parent )
	{
		Name = "Output";

		ItemActivated = ( a ) =>
		{
			if ( a is ICSharpCompiler.Diagnostic diagnostic )
				CodeEditor.OpenFile( diagnostic.FilePath, diagnostic.LineNumber, diagnostic.CharNumber );
		};

		ItemContextMenu = OpenItemContextMenu;
		ItemSize = new Vector2( 0, 48 );
		ItemSpacing = 0;
		Margin = 0;
	}

	/// <inheritdoc/>
	protected override void PaintItem( VirtualWidget item )
	{
		if ( item.Object is not ICSharpCompiler.Diagnostic diagnostic )
			return;

		var color = diagnostic.Severity switch
		{
			ICSharpCompiler.DiagnosticSeverity.Error => ProjectDiagnostics.ErrorColor,
			ICSharpCompiler.DiagnosticSeverity.Warning => ProjectDiagnostics.WarningColor,
			_ => ProjectDiagnostics.InfoColor
		};
		var icon = diagnostic.Severity switch
		{
			ICSharpCompiler.DiagnosticSeverity.Error => "error",
			ICSharpCompiler.DiagnosticSeverity.Warning => "warning",
			_ => "info"
		};

		Paint.SetBrush( color.WithAlpha( Paint.HasMouseOver ? 0.3f : 0.2f ).Darken( 0.4f ) );
		Paint.ClearPen();
		Paint.DrawRect( item.Rect.Shrink( 0, 1 ) );

		Paint.Antialiasing = true;
		Paint.SetPen( color.WithAlpha( Paint.HasMouseOver ? 1 : 0.7f ), 3.0f );
		Paint.ClearBrush();

		// Severity Icon
		var iconRect = item.Rect.Shrink( 12, 0 );
		iconRect.Width = 24;
		Paint.DrawIcon( iconRect, icon, 24 );

		var rect = item.Rect.Shrink( 48, 8, 0, 8 );

		Paint.SetPen( Theme.White.WithAlpha( Paint.HasMouseOver ? 1 : 0.8f ), 3.0f );
		Paint.DrawText( rect, diagnostic.Message, TextFlag.LeftTop | TextFlag.SingleLine );

		Paint.SetPen( Theme.White.WithAlpha( Paint.HasMouseOver ? 0.5f : 0.4f ), 3.0f );
		Paint.DrawText( rect, $"{diagnostic.Project} - {diagnostic.FilePath}({diagnostic.LineNumber},{diagnostic.CharNumber})", TextFlag.LeftBottom | TextFlag.SingleLine );
	}

	/// <summary>
	/// Opens a context menu for the error list entry.
	/// </summary>
	/// <param name="item">The error list entry.</param>
	private void OpenItemContextMenu( object item )
	{
		if ( item is not ICSharpCompiler.Diagnostic diagnostic )
			return;

		var menu = new Menu();

		menu.AddOption( "Open in Code Editor", "file_open", () => CodeEditor.OpenFile( diagnostic.FilePath, diagnostic.LineNumber, diagnostic.CharNumber ) );
		menu.AddOption( "Copy Error", "content_copy", () => Clipboard.Copy( diagnostic.Message ) );

		menu.OpenAt( Application.CursorPosition );
	}
}
