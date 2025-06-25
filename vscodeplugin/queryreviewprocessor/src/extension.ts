// The module 'vscode' contains the VS Code extensibility API
// Import the module and reference it with the alias vscode in your code below
import * as vscode from "vscode";

// This method is called when your extension is activated
// Your extension is activated the very first time the command is executed
export function activate(context: vscode.ExtensionContext) {
  // Use the console to output diagnostic information (console.log) and errors (console.error)
  // This line of code will only be executed once when your extension is activated
  console.log(
    'Congratulations, your extension "queryreviewprocessor" is now active!'
  );

  // The command has been defined in the package.json file
  // Now provide the implementation of the command with registerCommand
  // The commandId parameter must match the command field in package.json
  const disposable = vscode.commands.registerCommand(
    "queryreviewprocessor.helloWorld",
    async () => {
      // Open a new untitled SQL editor
      const doc = await vscode.workspace.openTextDocument({
        language: "sql",
        content: "-- Write your SQL here",
      });
      await vscode.window.showTextDocument(doc, { preview: false });
      // Optionally, show a message
      vscode.window.showInformationMessage(
        "SQL editor opened from query-review-processor!"
      );
    }
  );

  context.subscriptions.push(disposable);

  // Register the Execute SQL command
  const executeDisposable = vscode.commands.registerCommand(
    "queryreviewprocessor.executeSql",
    async () => {
      const editor = vscode.window.activeTextEditor;
      if (editor && editor.document.languageId === "sql") {
        const sql = editor.document.getText(
          editor.selection.isEmpty ? undefined : editor.selection
        );
        vscode.window.showInformationMessage(
          "Executing SQL: " + (sql ? sql : "[No SQL selected]")
        );
      } else {
        vscode.window.showWarningMessage("No active SQL editor found.");
      }
    }
  );
  context.subscriptions.push(executeDisposable);
}

// This method is called when your extension is deactivated
export function deactivate() {}
