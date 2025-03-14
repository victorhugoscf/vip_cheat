# Cheat_VIP ??  

Uma ferramenta de manipula��o de mem�ria desenvolvida em **C#** para modificar intru��es e comportamentos de processos em tempo real.  

---

## ? Funcionalidades  

- ?? **Anexar a Processos**: Conecte-se a um processo em execu��o para manipular sua mem�ria.  
- ?? **Substitui��o de Instru��es**: Modifique c�digos assembly em tempo real.  
- ?? **Inje��o de C�digo**: Insira instru��es personalizadas diretamente na mem�ria do processo.  
- ?? **Autentica��o via Pastebin**: Libere funcionalidades com credenciais do Pastebin.  
- ?? **Interface Gr�fica**: Interface intuitiva para facilitar a manipula��o.  

---

## ?? **Pr�-requisitos**  

- **.NET 8.0**  
- **Windows**  
- **Pastebin API Key** (para autentica��o)  
- **Permiss�es de Administrador** (para manipular mem�ria de processos)  

---

## ?? **Como Usar**  

### ?? 1. **Configura��o**  
Clone o reposit�rio:  

```bash
git clone https://github.com/victorhugoscf/Cheat_VIP.git
```

Abra o projeto no **Visual Studio** e configure a chave de API no arquivo `PastebinAuth.cs`:  

```csharp
private const string PastebinApiKey = "SUA_CHAVE_DE_API_AQUI";
```

### ?? 2. **Executando o Projeto**  
1?? Compile o projeto no **Visual Studio**.  
2?? Execute o aplicativo como **administrador**.  
3?? Fa�a login com suas credenciais do **Pastebin**.  
4?? Anexe-se a um processo e comece a manipular a mem�ria.  

---

## ?? **Exemplos de Uso**  

### ?? **Substituir uma Instru��o**  
Para substituir uma instru��o assembly, use:  

```csharp
memoryPatcher.ReplaceInstruction(address, valor, "eax", 5, 1); // Substitui 5 bytes e preenche 1 byte com NOP
```

### ?? **Restaurar o C�digo Original**  
Caso precise restaurar o c�digo original:  

```csharp
byte[] originalCode = new byte[] { 0x8B, 0x8D, 0x24, 0xFF, 0xFF, 0xFF }; // C�digo original
memoryPatcher.RestoreOriginalCode(address, originalCode);
```

---

## ?? **Interface Gr�fica**  

- ?? **Aba de Processos**: Liste e anexe-se a processos em execu��o.  
- ?? **Aba de Cheats**: Ative/desative hacks e modifique valores em tempo real.  
- ?? **Autentica��o**: Fa�a login via **Pastebin**.  

?? **Veja um exemplo da interface abaixo:**  

![Interface Gr�fica](interface.gif)


---

## ?? **Estrutura do Projeto**  

?? **Arquivos principais:**  

```
Cheat_VIP/
�-- src/
�   +-- Form1.cs            # Interface gr�fica principal
�   +-- MemoryPatcher.cs    # L�gica de manipula��o de mem�ria
�   +-- MemoryManager.cs    # Gerenciamento da conex�o com o processo
�   +-- PastebinAuth.cs     # Autentica��o via Pastebin
�-- README.md               # Documenta��o
```

---

## ?? **Contribuindo**  

Contribui��es s�o bem-vindas! Para colaborar:  

```bash
# 1?? Fa�a um fork do projeto
git clone https://github.com/victorhugoscf/Cheat_VIP.git

# 2?? Crie uma branch para sua feature
git checkout -b feature/nova-feature

# 3?? Commit suas mudan�as
git commit -m "Adicionando nova funcionalidade"

# 4?? Envie para o GitHub
git push origin feature/nova-feature
```

---

## ?? **Licen�a**  

Este projeto foi criado para fins educacionais, n�o me responsabilizo por uso indevido.  

---

## ?? **Contato**  

?? **Email:** [victorhugoscf@gmail.com](mailto:victorhugoscf@gmail.com)  
?? **GitHub:** [victorhugoscf](https://github.com/victorhugoscf)  

---

### ?? **Gostou do projeto?**  

? **Deixe uma estrela no reposit�rio!** ????  
