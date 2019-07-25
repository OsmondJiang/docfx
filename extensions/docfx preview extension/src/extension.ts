import * as vscode from 'vscode'
import * as request from 'request-promise-native'
import { exec } from 'child_process';

export function activate(context: vscode.ExtensionContext) {
    context.subscriptions.push(
        vscode.commands.registerCommand('docfx.watch', () => {
            PreviewPagePanel.CreateOrShow(context.extensionPath);
        })
    )
}

class WatchService{
    public static Start(){
        exec(`dotnet run D:/Work/Github/docfx/src/docfx --no-launch-profile --no-build --no-restore -- {watch-options}`)
    }
}

class PreviewPagePanel {
    public static currentPanel: PreviewPagePanel | undefined;

    public static readonly viewType = 'docfx preview';

	private readonly _panel: vscode.WebviewPanel;
	private readonly _pageUrl: string;
    private _disposables: vscode.Disposable[] = [];
    
    public static CreateOrShow(pageUrl: string){
        const column = vscode.window.activeTextEditor
			? vscode.window.activeTextEditor.viewColumn
			: undefined;

		// If we already have a panel, show it.
		if (PreviewPagePanel.currentPanel) {
			PreviewPagePanel.currentPanel._panel.reveal(column);
			return;
        }
        
        const panel = vscode.window.createWebviewPanel(
            PreviewPagePanel.viewType,
            'Docfx Preview',
            column || vscode.ViewColumn.One,
            {
                enableScripts: true,
                localResourceRoots: [vscode.Uri.file(pageUrl)]    
            }
        )

        PreviewPagePanel.currentPanel = new PreviewPagePanel(panel, pageUrl);
    }

    private constructor(panel: vscode.WebviewPanel, pageUrl: string){
        this._pageUrl = pageUrl;
        this._panel = panel;

        // show html
        this.showHtml();

        // Listen for when the panel is disposed
		// This happens when the user closes the panel or when the panel is closed programatically
        this._panel.onDidDispose(() => this.dispose(), null, this._disposables);
    }

    private showHtml(pageUrl: string){
        var options = {
            uri: "http://localhost:5001/" + pageUrl
        };

        var html = "H";
        request.get(options).then((result: string) => {html = result});

        this._panel.webview.html = html;
    }

    public dispose() {
		PreviewPagePanel.currentPanel = undefined;

		// Clean up our resources
		this._panel.dispose();

		while (this._disposables.length) {
			const x = this._disposables.pop();
			if (x) {
				x.dispose();
			}
		}
    }

}