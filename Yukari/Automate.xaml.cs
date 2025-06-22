using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OpenAI;
using OpenAI.Chat;


namespace Yukari;

public sealed partial class Automate : Page
{
    public MainWindow mainWindow = null;
    public TabViewItem tabItem = null;
    public TabViewItem tabViewItem = null;

    public List<HistoryItem> historyItems = [];
    public int activeIdx = -1;

    ChatTool writeToFileTool;
    ChatTool runShellCommandTool;
    ChatTool readFileTool;
    ChatCompletionOptions options;
    ChatClient client;

    public Automate()
    {
        InitializeComponent();

        writeToFileTool = ChatTool.CreateFunctionTool(
            functionName: "write_to_file",
            functionDescription: "指定されたファイルパスに文字列を書き込みます（上書き）。",
            functionParameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "path": {
                        "type": "string",
                        "description": "書き込み先ファイルのパス。例: 'hello.py'"
                    },
                    "content": {
                        "type": "string",
                        "description": "ファイルに書き込む文字列。"
                    }
                },
                "required": ["path", "content"],
                "additionalProperties": false
            }
            """)
        );

        runShellCommandTool = ChatTool.CreateFunctionTool(
            functionName: "run_shell_command",
            functionDescription: "指定されたシェルコマンドを実行し、その出力を返します。",
            functionParameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "command": {
                        "type": "string",
                        "description": "実行する PowerShell コマンド（例: 'Get-ChildItem'）"
                    }
                },
                "required": ["command"],
                "additionalProperties": false
            }
            """)
        );

        readFileTool = ChatTool.CreateFunctionTool(
            functionName: "read_file",
            functionDescription: "指定されたファイルパスから文字列を読み込みます。",
            functionParameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "path": {
                        "type": "string",
                        "description": "読み込み対象ファイルのパス。例: 'hello.py'"
                    }
                },
                "required": ["path"],
                "additionalProperties": false
            }
            """)
        );

        options = new()
        {
            Tools = { writeToFileTool, runShellCommandTool, readFileTool }
        };
    }

    void AddUserChatBox(string userMessage)
    {
        TextBlock textBlock = new()
        {
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.None,
            Margin = new Thickness(8),
            Text = userMessage
        };

        Border border = new()
        {
            Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
            Margin = new Thickness(32, 8, 8, 8),
            CornerRadius = new CornerRadius(4),
            HorizontalAlignment = HorizontalAlignment.Right,
            Child = textBlock
        };

        Chats.Children.Add(border);
    }

    void AddAssistantChatBox(string assistantMessage)
    {
        TextBlock textBlock = new()
        {
            TextWrapping = TextWrapping.WrapWholeWords,
            TextTrimming = TextTrimming.None,
            IsTextSelectionEnabled = true,
            Margin = new Thickness(8),
            Text = assistantMessage
        };

        Border border = new()
        {
            Background = (Brush)Application.Current.Resources["ControlFillColorDefaultBrush"],
            Margin = new Thickness(8, 8, 32, 8),
            CornerRadius = new CornerRadius(4),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = textBlock
        };

        Chats.Children.Add(border);
    }

    // コマンドを追加
    void AddAssistantToolChatBox(ChatToolCall toolCall)
    {
        StackPanel stackPanel = new();
        string function = toolCall.FunctionName;
        BinaryData argsJson = toolCall.FunctionArguments;
        using JsonDocument argsDoc = JsonDocument.Parse(argsJson);
        JsonElement root = argsDoc.RootElement;

        if (function == "run_shell_command")
        {
            string command = root.GetProperty("command").GetString();
            stackPanel = new StackPanel()
            {
                Margin = new Thickness(8),
                Children = {
                    new TextBlock()
                    {
                        TextWrapping = TextWrapping.WrapWholeWords,
                        TextTrimming = TextTrimming.None,
                        IsTextSelectionEnabled = true,
                        Margin = new Thickness(0),
                        FontWeight = FontWeights.Bold,
                        Text = Command.Text
                    },
                    new TextBlock()
                    {
                        TextWrapping = TextWrapping.WrapWholeWords,
                        TextTrimming = TextTrimming.None,
                        IsTextSelectionEnabled = true,
                        Margin = new Thickness(0, 8, 0, 0),
                        FontFamily = new FontFamily("Cascadia Code"),
                        Text = command
                    }
                }
            };
        }
        else if (function == "write_to_file")
        {
            string path = root.GetProperty("path").GetString();
            string content = root.GetProperty("content").GetString();
            stackPanel = new StackPanel()
            {
                Margin = new Thickness(8),
                Children = {
                    new TextBlock()
                    {
                        TextWrapping = TextWrapping.WrapWholeWords,
                        TextTrimming = TextTrimming.None,
                        IsTextSelectionEnabled = true,
                        Margin = new Thickness(0),
                        FontWeight = FontWeights.Bold,
                        Text = Path.Text
                    },
                    new TextBlock()
                    {
                        TextWrapping = TextWrapping.WrapWholeWords,
                        TextTrimming = TextTrimming.None,
                        IsTextSelectionEnabled = true,
                        Margin = new Thickness(0, 8, 0, 0),
                        Text = path
                    },
                    new TextBlock()
                    {
                        TextWrapping = TextWrapping.WrapWholeWords,
                        TextTrimming = TextTrimming.None,
                        IsTextSelectionEnabled = true,
                        Margin = new Thickness(0, 16, 0, 0),
                        FontWeight = FontWeights.Bold,
                        Text = FileContent.Text
                    },
                    new TextBlock()
                    {
                        TextWrapping = TextWrapping.WrapWholeWords,
                        TextTrimming = TextTrimming.None,
                        IsTextSelectionEnabled = true,
                        Margin = new Thickness(0, 8, 0, 0),
                        FontFamily = new FontFamily("Cascadia Code"),
                        Text = content
                    }
                }
            };
        }
        else if (function == "read_file")
        {
            string path = root.GetProperty("content").GetString();
            stackPanel = new StackPanel()
            {
                Margin = new Thickness(8),
                Children = {
                    new TextBlock()
                    {
                        TextWrapping = TextWrapping.WrapWholeWords,
                        TextTrimming = TextTrimming.None,
                        IsTextSelectionEnabled = true,
                        Margin = new Thickness(0),
                        FontWeight = FontWeights.Bold,
                        Text = Path.Text
                    },
                    new TextBlock()
                    {
                        TextWrapping = TextWrapping.WrapWholeWords,
                        TextTrimming = TextTrimming.None,
                        IsTextSelectionEnabled = true,
                        Margin = new Thickness(0, 8, 0, 0),
                        FontFamily = new FontFamily("Cascadia Code"),
                        Text = path
                    }
                }
            };
        }
        else
        {
            stackPanel = new StackPanel()
            {
                Margin = new Thickness(8),
                Children = {
                    new TextBlock() {
                        TextWrapping = TextWrapping.WrapWholeWords,
                        TextTrimming = TextTrimming.None,
                        IsTextSelectionEnabled = true,
                        Text = $"{function}\n{argsJson}"
                    }
                }
            };
        }

        Border border = new()
        {
            Background = (Brush)Application.Current.Resources["ControlFillColorDefaultBrush"],
            Margin = new Thickness(8, 8, 32, 8),
            CornerRadius = new CornerRadius(4),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = stackPanel
        };

        Chats.Children.Add(border);
    }

    List<OpenAI.Chat.ChatMessage> messages = [];

    public string SerializeMessagesToJson()
    {
        JsonSerializerOptions options = new()
        {
            WriteIndented = true
        };

        return JsonSerializer.Serialize(messages, options);
    }

    async Task HandleToolCallsAsync(ChatCompletion completion, string callId = null)
    {
        if (completion.FinishReason == ChatFinishReason.ToolCalls && completion.ToolCalls is { Count: > 0 })
        {
            foreach (ChatToolCall toolCall in completion.ToolCalls)
            {
                string name = toolCall.FunctionName;
                BinaryData argsJson = toolCall.FunctionArguments;
                string toolCallId = toolCall.Id;

                try
                {
                    using JsonDocument argsDoc = JsonDocument.Parse(argsJson);
                    JsonElement root = argsDoc.RootElement;

                    switch (name)
                    {
                        case "write_to_file":
                            {
                                string path = root.GetProperty("path").GetString();
                                string content = root.GetProperty("content").GetString();
                                
                                ContentDialog dialog = new()
                                {
                                    XamlRoot = mainWindow.Content.XamlRoot,
                                    Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                                    PrimaryButtonText = Accept.Text,
                                    SecondaryButtonText = Decline.Text,
                                    Title = AllowWriteThisContent.Text,
                                    DefaultButton = ContentDialogButton.Primary,
                                    Content = new StackPanel()
                                    {
                                        Children = {
                                            new TextBlock()
                                            {
                                                Margin = new Thickness(0, 0, 0, 0),
                                                Text = Path.Text,
                                                TextWrapping = TextWrapping.Wrap,
                                                FontWeight = FontWeights.Bold
                                            },
                                            new TextBlock()
                                            {
                                                Margin = new Thickness(0, 8, 0, 0),
                                                Text = path,
                                                TextWrapping = TextWrapping.Wrap,
                                            },
                                            new TextBlock()
                                            {
                                                Margin = new Thickness(0, 16, 0, 0),
                                                Text = FileContent.Text,
                                                TextWrapping = TextWrapping.Wrap,
                                                FontWeight = FontWeights.Bold
                                            },
                                            new TextBlock()
                                            {
                                                Margin = new Thickness(0, 8, 0, 0),
                                                Text = content,
                                                TextWrapping = TextWrapping.Wrap,
                                                FontFamily = new FontFamily("Cascadia Code")
                                            }
                                        }
                                    }
                                };
                                ContentDialogResult contentResult = await dialog.ShowAsync();
                                if (contentResult == ContentDialogResult.Secondary)
                                {
                                    string msg = "ユーザーによって拒否されました";
                                    Debug(msg);
                                    AddUserChatBox(msg);
                                    messages.Add(new SystemChatMessage(msg));
                                    await SendAIAsync();
                                    return;
                                }

                                await File.WriteAllTextAsync(path, content);
                                string result = $"{path} に書き込み完了しました。";
                                Debug(result);
                                AddUserChatBox(result);
                                messages.Add(new ToolChatMessage(callId, result));
                                await SendAIAsync();
                                break;
                            }

                        case "read_file":
                            {
                                string path = root.GetProperty("path").GetString();
                                string content = await File.ReadAllTextAsync(path);
                                string result = $"{path} の内容:\n{content}";
                                Debug(result);
                                AddUserChatBox(result);
                                messages.Add(new ToolChatMessage(callId, result));
                                await SendAIAsync();
                                break;
                            }

                        case "run_shell_command":
                            {
                                string command = root.GetProperty("command").GetString();
                                Debug($"コマンド \"{command}\" を実行します。");

                                ContentDialog dialog = new()
                                {
                                    XamlRoot = mainWindow.Content.XamlRoot,
                                    Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                                    PrimaryButtonText = Accept.Text,
                                    SecondaryButtonText = Decline.Text,
                                    Title = AllowThisCommand.Text,
                                    DefaultButton = ContentDialogButton.Primary,
                                    Content = new StackPanel()
                                    {
                                        Children = {
                                            new TextBlock()
                                            {
                                                Text = Command.Text,
                                                TextWrapping = TextWrapping.Wrap,
                                                FontWeight = FontWeights.Bold
                                            },
                                            new TextBlock()
                                            {
                                                Margin = new Thickness(0, 8, 0, 0),
                                                Text = command,
                                                TextWrapping = TextWrapping.Wrap,
                                                FontFamily = new FontFamily("Cascadia Code")
                                            }
                                        }
                                    }
                                };
                                ContentDialogResult contentResult = await dialog.ShowAsync();
                                if (contentResult == ContentDialogResult.Secondary)
                                {
                                    string msg = "ユーザーによって拒否されました";
                                    Debug(msg);
                                    AddUserChatBox(msg);
                                    messages.Add(new SystemChatMessage(msg));
                                    await SendAIAsync();
                                    return;
                                }

                                Process process = new()
                                {
                                    StartInfo = new ProcessStartInfo
                                    {
                                        FileName = "powershell",
                                        Arguments = $"-Command \"{command}\"",
                                        RedirectStandardOutput = true,
                                        RedirectStandardError = true,
                                        UseShellExecute = false,
                                        CreateNoWindow = true
                                    }
                                };

                                process.Start();
                                string stdout = await process.StandardOutput.ReadToEndAsync();
                                string stderr = await process.StandardError.ReadToEndAsync();
                                process.WaitForExit();

                                string result =
                                    $"コマンド \"{command}\" の実行が完了しました。\n\n" +
                                    $"終了コード:\n{process.ExitCode}\n\n" +
                                    $"標準出力:\n{stdout}\n\n" +
                                    $"標準エラー出力:\n{stderr}";

                                // Console.WriteLine(result);
                                AddUserChatBox(result);
                                Debug(result);
                                messages.Add(new ToolChatMessage(callId, result));
                                await SendAIAsync();
                                break;
                            }
                        default:
                            Debug($"未対応のツール: {name}");
                            return;
                            // throw new NotSupportedException($"未対応のツール: {name}");
                    }
                }
                catch (Exception ex)
                {
                    string error = $"ツール {name} の実行中にエラーが発生しました: {ex.Message}";
                    // Console.WriteLine(error);
                    Debug(error);
                    AddUserChatBox(error);
                    messages.Add(new SystemChatMessage(error));
                    await SendAIAsync();
                }
            }
        }
    }

    async Task SendAIAsync()
    {
        try
        {
            Debug(SerializeMessagesToJson());
            ChatCompletion completion = await client.CompleteChatAsync(messages, options);

            // 返答が存在する場合のみ追加
            if (completion?.Content != null && completion.Content.Count > 0)
            {
                string contentText = completion.Content[0].Text;
                AssistantChatMessage assistantChatMessage = new(contentText);
                AddAssistantChatBox(contentText);
                messages.Add(assistantChatMessage);
            }
            else if (completion?.ToolCalls != null && completion.ToolCalls.Count > 0)
            {
                // まず assistant 側の tool_calls を含んだメッセージを追加
                AssistantChatMessage assistantCallMessage = new(completion.ToolCalls);
                AddAssistantToolChatBox(completion.ToolCalls[0]);
                messages.Add(assistantCallMessage);
                string callId = completion.ToolCalls[0].Id;
                await HandleToolCallsAsync(completion, callId);
                return;
            }
            else
            {
                // contentが空のケースの処理
                Debug("Contentが空です。");
            }

            await HandleToolCallsAsync(completion);
        }
        catch (Exception ex)
        {
            Debug(ex.Message);
        }
    }

    void DisabledSendButton()
    {
        SendButton.IsEnabled = false;
    }

    void EnabledSendButton()
    {
        SendButton.IsEnabled = true;
    }

    async void Click_SendAsync(object sender, RoutedEventArgs e)
    {
        client = new ChatClient(model: MainWindow.GetModel(), credential: new ApiKeyCredential(MainWindow.GetApiKey()), new OpenAIClientOptions()
        {
            Endpoint = MainWindow.GetEndpoint()
        });
        string prompt = AutomateUserPrompt.Text;
        tabItem.Header = new TextBlock()
        {
            Text = prompt,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        AddUserChatBox(prompt);
        messages.Add(new UserChatMessage(prompt));
        DisabledSendButton();
        await SendAIAsync();
        EnabledSendButton();
    }

    async void Click_RetryAsync(object sender, RoutedEventArgs e)
    {
        client = new ChatClient(model: MainWindow.GetModel(), credential: new ApiKeyCredential(MainWindow.GetApiKey()), new OpenAIClientOptions()
        {
            Endpoint = MainWindow.GetEndpoint()
        });
        string prompt = Retry.Text;
        tabItem.Header = new TextBlock()
        {
            Text = prompt,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        AddUserChatBox(prompt);
        messages.Add(new UserChatMessage(prompt));
        DisabledSendButton();
        await SendAIAsync();
        EnabledSendButton();
    }

    void Debug(string txt) {
        if (File.Exists("debug.txt"))
            File.AppendAllText("debug.txt", txt);
    }
}