using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using Microsoft.Win32;

public class PowerPlanTray : ApplicationContext
{
    private NotifyIcon trayIcon;
    private ContextMenu trayMenu;
    private string appName = "PowerPlanTrayRaiz";

    public PowerPlanTray()
    {
        ConfigurarInicializacao();

        trayMenu = new ContextMenu();
        UpdateMenu();

        trayIcon = new NotifyIcon();
        trayIcon.Text = "Power Plan Tray";
        
        trayIcon.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); 
        trayIcon.ContextMenu = trayMenu;
        trayIcon.Visible = true;
    }

    private void ConfigurarInicializacao()
    {
        try
        {
            string currentPath = Application.ExecutablePath;
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
            {
                object currentValue = key.GetValue(appName);
                if (currentValue == null || currentValue.ToString() != currentPath)
                {
                    key.SetValue(appName, currentPath);
                }
            }
        }
        catch { }
    }

    private void UpdateMenu()
    {
        trayMenu.MenuItems.Clear();
        List<PowerPlan> plans = GetPowerPlans();
        
        foreach (PowerPlan plan in plans)
        {
            string currentGuid = plan.Guid; 
            MenuItem item = new MenuItem(plan.Name, (s, e) => SetPowerPlan(currentGuid));
            if (plan.IsActive) item.Checked = true;
            trayMenu.MenuItems.Add(item);
        }

        trayMenu.MenuItems.Add("-");

        MenuItem menuGerenciar = new MenuItem("Gerenciar Planos...");
        menuGerenciar.MenuItems.Add("Criar Novo (Cópia do Atual)", (s, e) => CriarNovoPlano(plans));
        
        MenuItem menuDeletar = new MenuItem("Deletar Plano");
        bool temPlanoParaDeletar = false;
        
        foreach (PowerPlan plan in plans)
        {
            if (!plan.IsActive) 
            {
                string guidParaDeletar = plan.Guid;
                menuDeletar.MenuItems.Add(plan.Name, (s, e) => DeletarPlano(guidParaDeletar));
                temPlanoParaDeletar = true;
            }
        }
        
        if (!temPlanoParaDeletar) menuDeletar.Enabled = false;
        
        menuGerenciar.MenuItems.Add(menuDeletar);
        
        menuGerenciar.MenuItems.Add("-");
        menuGerenciar.MenuItems.Add("Abrir Painel do Windows", (s, e) => Process.Start("control.exe", "powercfg.cpl"));

        trayMenu.MenuItems.Add(menuGerenciar);

        trayMenu.MenuItems.Add("-");
        trayMenu.MenuItems.Add("Atualizar Lista", (s, e) => UpdateMenu());
        trayMenu.MenuItems.Add("Sair", (s, e) => Exit());
    }

    private void SetPowerPlan(string guid)
    {
        ExecutarComando("powercfg", "/s " + guid);
        UpdateMenu(); 
    }

    private void CriarNovoPlano(List<PowerPlan> plans)
    {
        PowerPlan activePlan = plans.Find(p => p.IsActive);
        if (activePlan == null) return;

        string novoNome = SolicitarNomePlano();
        if (string.IsNullOrWhiteSpace(novoNome)) return;

        ProcessStartInfo psi = new ProcessStartInfo("powercfg", "/duplicatescheme " + activePlan.Guid);
        psi.RedirectStandardOutput = true;
        psi.CreateNoWindow = true;
        psi.UseShellExecute = false;
        
        string newGuid = "";
        using (Process p = Process.Start(psi))
        {
            string output = p.StandardOutput.ReadToEnd();
            // LÓGICA UNIVERSAL: Quebra a resposta em palavras e testa qual delas tem formato de GUID
            string[] words = output.Split(new char[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string word in words)
            {
                Guid tempGuid;
                if (Guid.TryParse(word, out tempGuid))
                {
                    newGuid = word;
                    break;
                }
            }
        }

        if (!string.IsNullOrEmpty(newGuid))
        {
            ExecutarComando("powercfg", string.Format("/changename {0} \"{1}\"", newGuid, novoNome));
            UpdateMenu();
        }
    }

    private void DeletarPlano(string guid)
    {
        DialogResult confirm = MessageBox.Show("Tem certeza que deseja deletar permanentemente este plano de energia?", "Deletar Plano", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm == DialogResult.Yes)
        {
            ExecutarComando("powercfg", "/delete " + guid);
            UpdateMenu();
        }
    }

    private string SolicitarNomePlano()
    {
        Form prompt = new Form()
        {
            Width = 400, Height = 150, FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = "Novo Plano de Energia", StartPosition = FormStartPosition.CenterScreen,
            MinimizeBox = false, MaximizeBox = false
        };
        
        Label textLabel = new Label() { Left = 20, Top = 20, Text = "Nome para o novo plano (baseado no atual):", Width = 340 };
        TextBox textBox = new TextBox() { Left = 20, Top = 45, Width = 340 };
        Button confirmation = new Button() { Text = "Criar", Left = 260, Width = 100, Top = 75, DialogResult = DialogResult.OK };
        
        prompt.Controls.Add(textBox);
        prompt.Controls.Add(confirmation);
        prompt.Controls.Add(textLabel);
        prompt.AcceptButton = confirmation;

        return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : "";
    }

    private void ExecutarComando(string file, string args)
    {
        ProcessStartInfo psi = new ProcessStartInfo(file, args);
        psi.CreateNoWindow = true;
        psi.UseShellExecute = false;
        Process.Start(psi).WaitForExit();
    }

    private List<PowerPlan> GetPowerPlans()
    {
        List<PowerPlan> list = new List<PowerPlan>();
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo("powercfg", "/l");
            psi.RedirectStandardOutput = true;
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            
            using (Process p = Process.Start(psi))
            {
                string output = p.StandardOutput.ReadToEnd();
                string[] lines = output.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                
                foreach (string line in lines)
                {
                    string guid = null;
                    
                    // LÓGICA UNIVERSAL: Avalia palavra por palavra da linha
                    string[] words = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string word in words)
                    {
                        Guid tempGuid;
                        // Se a palavra for um GUID válido (formato xxxxxxxx-xxxx...),salva
                        if (Guid.TryParse(word, out tempGuid))
                        {
                            guid = word;
                            break;
                        }
                    }

                    // Se achou um GUID na linha, plano válido
                    if (guid != null)
                    {
                        int startParentheses = line.IndexOf('(');
                        int endParentheses = line.LastIndexOf(')');
                        string name = "Plano Desconhecido";
                        
                        if (startParentheses != -1 && endParentheses != -1 && endParentheses > startParentheses)
                            name = line.Substring(startParentheses + 1, endParentheses - startParentheses - 1);
                        
                        bool isActive = line.Contains("*");
                        
                        list.Add(new PowerPlan { Guid = guid, Name = name, IsActive = isActive });
                    }
                }
            }
        }
        catch { }
        return list;
    }

    private void Exit()
    {
        trayIcon.Visible = false; 
        Application.Exit();
    }

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new PowerPlanTray());
    }
}

public class PowerPlan
{
    public string Guid { get; set; }
    public string Name { get; set; }
    public bool IsActive { get; set; }
}
