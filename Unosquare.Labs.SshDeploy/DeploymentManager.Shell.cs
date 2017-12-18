namespace Unosquare.Labs.SshDeploy
{
    using Options;
    using Swan;
    using System;
    using System.Text;

    public partial class DeploymentManager
    {
        public static void ExecuteShellVerb(ShellVerbOptions invokedVerbOptions)
        {
            using (var sshClient = CreateClient(invokedVerbOptions))
            {
                sshClient.Connect();

                var encoding = Encoding.ASCII;

                using (var shell = CreateBaseShellStream(sshClient))
                {
                    shell.DataReceived += OnShellDataRx;

                    shell.ErrorOccurred += (s, e) => e.Exception.Message.Debug();

                    _forwardShellStreamOutput = true;

                    while (true)
                    {
                        var line = Console.ReadLine();
                        var lineData = encoding.GetBytes(line + "\r\n");
                        shell.Write(lineData, 0, lineData.Length);
                        shell.Flush();

                        if (!line.Equals("exit")) continue;

                        var expectResult = shell.Expect("logout", TimeSpan.FromSeconds(2));
                        if (string.IsNullOrWhiteSpace(expectResult) == false && expectResult.Trim().EndsWith("logout"))
                        {
                            break;
                        }
                    }
                }

                sshClient.Disconnect();
            }
        }
    }
}