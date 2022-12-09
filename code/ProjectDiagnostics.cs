using Editor;
using Sandbox;
using Sandbox.Internal;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Gooman.Tools.ProjectDiagnostics;

/// <summary>
/// An improved version of the default Sbox error list.
/// </summary>
[Dock( "Editor", "Project Diagnostics", "report" )]
public sealed class ProjectDiagnostics : Widget
{
	/// <summary>
	/// The information color according to the current theme.
	/// </summary>
	internal static Color InfoColor => Theme.Blue;
	/// <summary>
	/// The warning color according to the current theme.
	/// </summary>
	internal static Color WarningColor => Theme.Yellow;
	/// <summary>
	/// The error color according to the current theme.
	/// </summary>
	internal static Color ErrorColor => Theme.Red;

	/// <summary>
	/// The current list of diagnostics from all compile groups.
	/// </summary>
	private static readonly List<ICSharpCompiler.Diagnostic> Diagnostics = new();
	/// <summary>
	/// The current set of all project names from compile groups.
	/// </summary>
	private static readonly HashSet<string> Projects = new();

	/// <summary>
	/// The information toggle button.
	/// </summary>
	private readonly Button InfoButton;
	/// <summary>
	/// The warnings toggle button.
	/// </summary>
	private readonly Button WarningsButton;
	/// <summary>
	/// The errors toggle button.
	/// </summary>
	private readonly Button ErrorsButton;
	/// <summary>
	/// The combo box for diagnostics filtering.
	/// </summary>
	private readonly ComboBox ProjectFilterBox;
	/// <summary>
	/// The visual list of all diagnostics.
	/// </summary>
	private readonly DiagnosticsListView DiagnosticsView;
	/// <summary>
	/// A static label notifying the user the <see cref="DiagnosticsView"/> is empty.
	/// </summary>
	private readonly Label EmptyErrorListLabel;

	/// <summary>
	/// Whether or not to show all information.
	/// </summary>
	private bool ShowInfo = true;
	/// <summary>
	/// Whether or not to show all warnings.
	/// </summary>
	private bool ShowWarnings = true;
	/// <summary>
	/// Whether or not to show all errors.
	/// </summary>
	private bool ShowErrors = true;
	/// <summary>
	/// The current filter for projects.
	/// </summary>
	private string FilteredProject = "all";

	/// <summary>
	/// Initializes a new instance of <see cref="ProjectDiagnostics"/>.
	/// </summary>
	/// <param name="parent">The parent containing this element.</param>
	public ProjectDiagnostics( Widget parent ) : base( parent )
	{
		SetStyles( Styles );
		Name = "ProjectDiagnostics";

		Layout = Layout.Column();

		var layout = Layout.Add( Layout.Row() );
		layout.Spacing = 8;
		layout.Margin = 5;

		// Diagnostic toggle buttons.
		ErrorsButton = new Button( "0 Errors", "error", this )
		{
			Clicked = () =>
			{
				ShowErrors = !ShowErrors;
				Cookie.Set( "project_diag_errors_shown", ShowErrors );
				UpdateErrors();
				ErrorsButton.Update();
			},
			OnPaintOverride = () => PaintButton( ErrorsButton, "error", ErrorColor, ShowErrors ),
			StatusTip = "Toggle display of errors",
		};

		WarningsButton = new Button( "0 Warnings", "warning", this )
		{
			Clicked = () =>
			{
				ShowWarnings = !ShowWarnings;
				Cookie.Set( "project_diag_warnings_shown", ShowInfo );
				UpdateErrors();
				WarningsButton.Update();
			},
			OnPaintOverride = () => PaintButton( WarningsButton, "warning", WarningColor, ShowWarnings ),
			StatusTip = "Toggle display of warnings",
		};

		InfoButton = new Button( "0 Messages", "info", this )
		{
			Clicked = () =>
			{
				ShowInfo = !ShowInfo;
				Cookie.Set( "project_diag_info_shown", ShowInfo );
				UpdateErrors();
				InfoButton.Update();
			},
			OnPaintOverride = () => PaintButton( InfoButton, "info", InfoColor, ShowInfo ),
			StatusTip = "Toggle display of information",
		};

		layout.Add( ErrorsButton );
		layout.Add( WarningsButton );
		layout.Add( InfoButton );

		layout.AddStretchCell();

		// Misc elements.
		ProjectFilterBox = new ComboBox( this )
		{
			StatusTip = "Filter what projects diagnostics to show"
		};
		ResetFilterBox();

		var clearButton = new Button( string.Empty, "delete", this )
		{
			ButtonType = "clear",
			Clicked = Reset,
			StatusTip = "Clear error list"
		};
		clearButton.SetProperty( "cssClass", "clear" );

		layout.Add( ProjectFilterBox );
		layout.Add( clearButton );

		// Error list.
		DiagnosticsView = new DiagnosticsListView( this );
		Layout.Add( DiagnosticsView, 1 );

		EmptyErrorListLabel = new Label.Title( "No diagnostics to show", this )
		{
			Alignment = TextFlag.Center
		};
		Layout.Add( EmptyErrorListLabel, 1 );

		// Load persistent options.
		ShowInfo = Cookie.Get( "project_diag_info_shown", true );
		ShowWarnings = Cookie.Get( "project_diag_warnings_shown", true );
		ShowErrors = Cookie.Get( "project_diag_errors_shown", true );
		// Update.
		UpdateErrors();
	}

	/// <summary>
	/// Invoked when a group of compilers have finished compiling.
	/// </summary>
	/// <param name="compileGroup">The group of compilers that finished.</param>
	[Event( "compile.complete" )]
	private void CaptureDiagnostics( ICSharpCompiler.Group compileGroup )
	{
		// Reset error list and add new diagnostics.
		Reset();
		Diagnostics.AddRange( compileGroup.Compilers.Where( x => x.Diagnostics is not null ).SelectMany( x => x.Diagnostics ) );

		// Add unique projects to the filter box.
		var projectNames = Diagnostics.Select( diag => diag.Project ).Distinct();
		foreach ( var projectName in projectNames )
		{
			var added = Projects.Add( projectName );
			if ( added )
				ProjectFilterBox.AddItem( projectName, null, () => UpdateErrors( projectName ) );
		}

		// Grab a total count of all errors, update status bar and pop up errors list if they have some
		var errors = Diagnostics.Where( a => a.Severity == ICSharpCompiler.DiagnosticSeverity.Error ).ToArray();
		if ( errors.Length > 0 )
			EditorWindow?.StatusBar.ShowMessage( $"Build failed - you have {errors.Length} errors", 10 );
	}

	/// <summary>
	/// Invoked when a group of compilers have finished compiling.
	/// </summary>
	/// <param name="_">Ignore.</param>
	[Event( "compile.complete", Priority = 10 )]
	private void OnCompileComplete( ICSharpCompiler.Group _ )
	{
		// CaptureDiagnostics fills in the static Diagnostics list
		// which is the diagostics from the most recent compile.
		// which is all we care about, really
		UpdateErrors();
	}

	/// <summary>
	/// Resets the diagnostic and project elements in the error list.
	/// </summary>
	private void Reset()
	{
		Projects.Clear();
		ResetFilterBox();
		Diagnostics.Clear();
		UpdateErrors();
	}

	/// <summary>
	/// Resets the <see cref="ProjectFilterBox"/> and adds the "All Projects" option.
	/// </summary>
	private void ResetFilterBox()
	{
		ProjectFilterBox.Clear();
		ProjectFilterBox.AddItem( "All Projects", null, () => UpdateErrors( "all" ) );
	}

	/// <summary>
	/// Updates the diagnostic list.
	/// </summary>
	/// <param name="projectName">If provided, sets the project name filter.</param>
	/// <param name="hideUnaffiliatedDiagnostics">If provided, sets whether or not to hide unaffiliated diagnostics.</param>
	private void UpdateErrors( string projectName = null )
	{
		if ( projectName is not null )
			FilteredProject = projectName;

		// Fast path
		if ( Diagnostics.Count == 0 || (!ShowInfo && !ShowWarnings && !ShowErrors) )
		{
			DiagnosticsView.SetItems( Array.Empty<object>() );
			DiagnosticsView.Hide();
			EmptyErrorListLabel.Show();
			return;
		}

		var q = Diagnostics.Where( FilterDiagnostic );

		InfoButton.Text = $"{q.Count( x => x.Severity == ICSharpCompiler.DiagnosticSeverity.Info )} Messages";
		WarningsButton.Text = $"{q.Count( x => x.Severity == ICSharpCompiler.DiagnosticSeverity.Warning )} Warnings";
		ErrorsButton.Text = $"{q.Count( x => x.Severity == ICSharpCompiler.DiagnosticSeverity.Error )} Errors";

		if ( !ShowInfo )
			q = q.Where( x => x.Severity != ICSharpCompiler.DiagnosticSeverity.Info );
		if ( !ShowWarnings )
			q = q.Where( x => x.Severity != ICSharpCompiler.DiagnosticSeverity.Warning );
		if ( !ShowErrors )
			q = q.Where( x => x.Severity != ICSharpCompiler.DiagnosticSeverity.Error );

		q = q.OrderByDescending( x => x.Severity == ICSharpCompiler.DiagnosticSeverity.Error );

		DiagnosticsView.SetItems( q.Cast<object>() );
		if ( q.Any() )
		{
			DiagnosticsView.Show();
			EmptyErrorListLabel.Hide();
		}
		else
		{
			DiagnosticsView.Hide();
			EmptyErrorListLabel.Show();
		}
	}

	/// <summary>
	/// Returns whether or not to show a diagnostic.
	/// </summary>
	/// <param name="diagnostic">The diagnostic to filter.</param>
	/// <returns>Whether or not to show the diagnostic.</returns>
	private bool FilterDiagnostic( ICSharpCompiler.Diagnostic diagnostic )
	{
		if ( diagnostic.Severity == ICSharpCompiler.DiagnosticSeverity.Hidden )
			return false;

		if ( FilteredProject != "all" && diagnostic.Project != FilteredProject )
			return false;

		return true;
	}

	/// <summary>
	/// Paints a button.
	/// </summary>
	/// <param name="button">The button to paint.</param>
	/// <param name="icon">The icon to display in the button.</param>
	/// <param name="color">The highlight color of the button.</param>
	/// <param name="active">Whether or not the button is active.</param>
	/// <returns>True.</returns>
	private static bool PaintButton( Button button, string icon, Color color, bool active )
	{
		var rect = button.LocalRect;

		Paint.SetBrush( Theme.Primary.WithAlpha( Paint.HasMouseOver ? 0.2f : 0.1f ) );
		Paint.ClearPen();

		if ( active )
		{
			Paint.SetPen( Theme.Primary.WithAlpha( 0.4f ), 2.0f );
			Paint.DrawRect( rect, 2 );
		}

		rect = rect.Shrink( 8, 3 );

		Paint.Antialiasing = true;
		Paint.SetPen( color.WithAlpha( Paint.HasMouseOver ? 1 : 0.7f ), 3.0f );
		Paint.ClearBrush();

		// Severity Icon
		var iconRect = rect;
		iconRect.Left += 0;
		iconRect.Width = 16;
		Paint.DrawIcon( iconRect, icon, 16 );

		rect.Left = iconRect.Right + 2;
		Paint.SetDefaultFont();
		Paint.SetPen( Theme.White.WithAlpha( active ? 1 : 0.4f ), 3.0f );
		Paint.DrawText( rect, button.Text, TextFlag.Center );

		return true;
	}

	/// <summary>
	/// The style sheet to use in the widget.
	/// </summary>
	private const string Styles = @"
	ProjectDiagnostics #Output {
		margin: 0px;
		padding: 0px;
		border: 0px;
		padding: 0px;
		margin-bottom: 4px;
	}

	ProjectDiagnostics QPushButton {
		padding: 3px 8px;
	}

	ProjectDiagnostics QPushButton[cssClass=""clear""] {
		padding: 3px;
	}

	ProjectDiagnostics QComboBox {
		width: 9em;
		max-height: 16px;
		min-height: 16px;
		padding: 2px 8px;
	}";
}
