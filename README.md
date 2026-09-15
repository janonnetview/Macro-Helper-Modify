# SK MacroHelper

Expansor de texto para Windows. Guarda os textos que você repete — respostas de chamado,
modelos, blocos de contrato — e insere qualquer um deles em qualquer programa, sem copiar e
colar. Junto vêm tarefas com lembrete e notas.

**Tudo local.** Os dados ficam num único arquivo SQLite no seu computador. Não há login,
servidor nem sincronização: o app funciona com o Wi-Fi desligado.

---

## Como se usa

| O quê | Como |
|---|---|
| Inserir uma macro | Digite `/atalho` em qualquer campo de texto do Windows. Setas para escolher no popup, `Enter` para inserir, `Esc` para fechar. |
| Procurar pelo título | `Ctrl+Espaço` em qualquer lugar, mesmo com a janela fechada na bandeja. |
| Ver tarefas, notas e o que você copiou | No mesmo `Ctrl+Espaço`: `Tab` alterna entre **Macros**, **Tarefas**, **Notas** e **Copiados**. Abre sempre em Macros. |
| Atalho dedicado | `Ctrl+Alt+1` a `Ctrl+Alt+9`, configurável por macro. |
| Desfazer a última inserção | `Ctrl+Shift+Z` |
| Repetir a última | `Ctrl+Alt+0` |

### Dentro do conteúdo de uma macro

| Escreve | Faz |
|---|---|
| `{variavel}` | pergunta o valor na hora de inserir, ou usa o de **Variáveis Globais** se estiver cadastrado lá |
| `{cliente:Fulano}` | idem, já vindo preenchido com "Fulano" |
| `{status:Aberto\|Em análise\|Concluído}` | vira uma **lista para escolher** em vez de uma caixa de texto |
| `{macro:outro-atalho}` | insere o conteúdo de outra macro, resolvido em cadeia |
| `{\|}` | onde o cursor para depois da inserção — `{cursor}` é o mesmo, por extenso |
| `{data}` `{hora}` `{datahora}` `{mes}` `{usuario}` `{clipboard}` | preenchidas sozinhas, sem perguntar nada |
| `{data+2}` `{data-1}` | daqui a dois dias · ontem |
| `{data+3u}` | daqui a três dias **úteis** — pula sábado e domingo |
| `{data+2s}` `{mes+1}` `{hora+2}` `{hora+90m}` | semanas · meses · horas · minutos |
| `{data:dd/MM}` `{data+1:dddd}` | formato próprio, no padrão do .NET |

A barra vertical é o que distingue uma lista de opções de um formato: depois dos dois-pontos,
`|` significa lista. Nas variáveis de data e hora não existe lista — ali o que vem depois dos
dois-pontos é sempre formato.

### A janela do `Ctrl+Espaço`

Quatro abas e uma busca só. `Enter` faz o que faz sentido para o item selecionado:

- **macro** vai como texto para o programa onde você estava;
- **tarefa** é marcada como concluída, e a janela continua aberta para a próxima. `F2` abre
  a tarefa para editar ao lado, com observações, prioridade, prazo, lembrete e repetição;
- **nota** abre numa segunda janela, ao lado. A busca continua na tela: andar na lista —
  de clique ou de seta — troca a nota mostrada, sem fechar nem voltar. O que você escrever
  é gravado ao fechar (`Esc`), ao trocar de nota ou quando o foco sai — não existe caminho
  que descarte o texto, e abrir uma nota só para ler não mexe na data de edição dela.
  `Ctrl+S` grava sem fechar. `Ctrl+Enter` manda a nota para o programa de destino, e
  funciona também na lista, sem abrir;
- **copiado** volta para o programa onde você estava, **sem passar pela área de transferência**
  — colar do histórico não custa o que estiver copiado agora.

Em Tarefas e Notas dá para **criar** (`Ctrl+N`, ou o botão **+ Nova** ao lado das abas) e
**apagar** (botão **Apagar** dentro do item, que pede um segundo clique para confirmar). Não é
preciso abrir o app para nada disso. Macro fica de fora de propósito: ela tem atalho, categoria
e histórico de versões, e isso não cabe numa janela de atalho.

Nota entra no outro programa como está escrita: `{campo}` dentro de uma nota é chave e
colchete, não variável a preencher.

### Área de transferência

A aba **Copiados** guarda os últimos 50 textos copiados em qualquer programa, buscáveis (a
busca ignora acento e caixa). O alfinete fixa um item no topo — **fixado nunca é descartado
pelo limite**. `Shift+Delete` apaga o selecionado; **Limpar** esvazia o resto, preservando os
fixados.

Só texto é guardado, tudo no arquivo local do app. O que um gerenciador de senhas copia **não**
entra: o app respeita os dois formatos de exclusão que o KeePass, o 1Password e o Bitwarden
marcam, e que o próprio Win+V do Windows obedece. Para desligar a captura por completo:
**Configurações › Gatilho › Histórico da área de transferência** — desligar para de capturar e
não apaga o que já existe.

### Tarefas

Têm **prazo** (informativo) e **lembrete** (a notificação). São campos independentes: querer
ser avisado na quarta sobre algo que vence na quinta é o caso normal. O lembrete só aparece com
o app aberto — pode estar minimizado na bandeja.

**Clicar no balão do lembrete abre a tarefa**, com os botões de adiar 15 min, 1 hora ou amanhã
às 9h. Adiar rearma a notificação: ela volta a aparecer no horário novo.

**Repetir** cria a próxima ocorrência ao concluir — todo dia, dias úteis, toda semana, a cada
15 dias, todo mês ou todo ano. Prazo e lembrete são deslocados **juntos**, então "me avise na
véspera às 9h" continua valendo na ocorrência seguinte. Concluir com atraso não gera uma tarefa
já vencida: a próxima cai no futuro. A ocorrência concluída fica no histórico como tarefa
comum — é a nova que herda a repetição.

### Lembretes: lista, semana ou mês

A tela abre em **Lista**, e os botões no alto da direita trocam para **Semana** e **Mês**. A
escolha fica gravada: quem trabalha na grade abre o app nela.

O que a grade acrescenta é uma coisa só, e é o motivo dela existir: **choque de horário se
vê**. Dois compromissos que se cruzam aparecem lado a lado, cada um com metade da largura do
dia e a borda em âmbar, e o dia ganha um sinal de aviso no cabeçalho. Antes isso era uma frase
no aviso de quem acabava de salvar, e sumia em seguida.

- As setas andam uma semana (ou um mês); **Hoje** volta para o presente.
- Na semana, **clicar num horário vazio** abre o formulário já naquele dia e hora, de meia em
  meia hora. Clicar num compromisso abre ele.
- No mês (e no cabeçalho do dia, na semana), **clicar no dia** abre o formulário naquela data,
  às 9h.
- A faixa de horas da semana cresce para caber o que existe: um compromisso às 6h30 puxa o topo
  da grade, sem espremer o resto do dia.
- Lembrete **sem data** não cabe em grade nenhuma, então uma tarja conta quantos são e leva de
  volta para a lista. Ele não some.

### Notas formatadas

A nota é escrita **como ela fica**: negrito é negrito, título é título, caixa de marcar é uma
caixinha. Não há marcação à vista enquanto se escreve.

A barra acima do editor formata o que estiver selecionado, e **selecionar um trecho abre a
mesma barra em cima dele**, como no Word. De teclado: `Ctrl+B` negrito, `Ctrl+I` itálico,
`Ctrl+K` link (pergunta o endereço, já preenchido com o que estiver copiado),
`Ctrl+Shift+X` riscado, `Ctrl+Shift+E` código. O `Ω` abre a grade de caracteres especiais
(`✓`, `→`, `—`, `±`...) e insere no cursor.

O que a barra sabe fazer: títulos em três tamanhos, lista, lista numerada, caixa de marcar,
citação, link, código, divisor e negrito/itálico/riscado dentro da linha. O `Enter` no fim de
um item continua a lista (e o `Enter` num item vazio sai dela), clicar na caixinha marca a
tarefa, e `Ctrl+clique` num link abre o navegador.

Tudo isso vale igual na **nota flutuante** do `Ctrl+Espaço`: a mesma barra e os mesmos
atalhos, mais o botão de olho no cabeçalho, que mostra a nota em modo de leitura.

**No banco a nota continua sendo texto**, em Markdown (`## título`, `- [ ] item`,
`**negrito**`). É isso que a busca lê, que o cartão resume e que vai para o outro programa
quando você manda inserir — e é por isso que uma nota escrita em Markdown noutro lugar pode
ser colada aqui e aparece formatada. Duas diferenças para o Markdown clássico, de propósito:
**cada linha é uma linha** (duas seguidas não viram um parágrafo só) e a formatação **não
aninha**. Colar de fora entra como texto, sem trazer a formatação do Word ou do navegador.

---

## Onde ficam os dados

| O quê | Caminho |
|---|---|
| Banco de dados | `%AppData%\MacroHelper\macrohelper.db` |
| Log de erros | `%AppData%\MacroHelper\erros.log` |
| Preferências | `Properties.Settings` do usuário (tema, nome, atalhos) |

**Backup:** feche o app e copie `macrohelper.db`. É um arquivo só.

O esquema é versionado em [`MacroHelper.Data/Sql/*.sql`](MacroHelper.Data/Sql) e aplicado no
startup pelo `DatabaseMigrator`, que controla a versão em `PRAGMA user_version`. Para criar uma
tabela nova, acrescente `006_*.sql` (a 005 é a última aplicada) — **nunca edite um
script já aplicado.**

---

## Rodar do código

Precisa do [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). Windows só — a UI é
WPF e o app usa hook de teclado e bandeja do sistema.

```powershell
dotnet run   --project MacroHelper.UI     # roda em modo dev
dotnet build MacroHelper.sln -c Release   # só compila
dotnet test  MacroHelper.sln              # testes, sem rede
```

**Warning é erro.** `Directory.Build.props` liga `TreatWarningsAsErrors` para toda a solution:
ou o aviso some, ou é suprimido com um `NoWarn` e um comentário dizendo por quê.

## Testes

`dotnet test` roda em segundos, offline e sem nenhuma variável de ambiente. Cada teste de
repositório cria um arquivo SQLite descartável e aplica as migrações reais — não há mock de
banco, e é o esquema de verdade que está sendo exercitado.

O que a suíte cobre, além do caminho feliz:

- os índices únicos recusam atalho repetido (inclusive quando só a caixa difere) e categoria
  raiz duplicada;
- `ON DELETE SET NULL` e `ON DELETE CASCADE` fazem o que o esquema diz — o que também prova
  que `PRAGMA foreign_keys` está ligado em toda conexão;
- datas voltam com o mesmo horário de parede, sem deslocamento de fuso;
- a busca acha "Ação" procurando "acao" — em macro, tarefa, nota e no histórico de cópias —
  e `%` não vira curinga;
- um lembrete que venceu com o app fechado dispara na primeira varredura, uma vez só;
- adiar rearma um lembrete que já disparou, e conta a partir de agora, não do horário antigo;
- uma tarefa semanal concluída com três semanas de atraso vai para a semana que vem, não para
  a que passou; "todo dia 31" não vira "todo dia 28" depois de fevereiro; concluí-la de novo
  depois de reabrir não cria uma segunda cópia da mesma ocorrência;
- copiar o mesmo texto duas vezes não duplica a linha no histórico, a poda por limite não
  alcança o que está fixado, e o `\r` do CRLF não conta ao posicionar o cursor com `{|}`;
- `{data+3u}` pula o fim de semana, `{data}` e `{data+7}` são dois campos distintos, e nem
  `{cursor}` nem `{macro:x}` viram pergunta ao usuário;
- duplicar uma macro não copia o atalho de teclado (que é único) nem o favorito, e traz a
  imagem que as listagens não carregam;
- todo `{Binding}` das telas aponta para uma propriedade que existe, e toda chave de recurso
  usada está definida nos dois temas;
- a janela do `Ctrl+Espaço` abre na aba de macros e sabe desenhar os quatro tipos de item;
- a nota aberta ao lado carrega o texto inteiro, troca de nota gravando a anterior, e fechar
  sem ter editado não regrava — o que manteria a nota pulando para o topo da lista;
- criar sem escrever nada não grava linha nenhuma, apagar exige o segundo clique, e uma data
  digitada pela metade mantém a que já estava em vez de apagá-la em silêncio.

## Gerar o instalador

```powershell
dotnet publish MacroHelper.UI -c Release -r win-x64 --self-contained true
```

Depois abra `setup.iss` no [Inno Setup 6](https://jrsoftware.org/isdl.php) e compile (`F9`).
A versão do instalador é lida do `.exe` publicado — publique antes, ou a compilação para com
uma mensagem dizendo isso. Passo a passo em [CLAUDE.md](CLAUDE.md).

---

## Estrutura

| Projeto | O que tem dentro |
|---|---|
| `MacroHelper.UI` | WPF · .NET 8 — telas, ViewModels, hook de teclado, bandeja |
| `MacroHelper.Services` | Regras de negócio (`Dominio/`) e interop com o Windows (`Windows/`) |
| `MacroHelper.Data` | SQLite + Dapper — repositórios, scripts de esquema, migrator |
| `MacroHelper.Core` | Entidades e utilitários sem dependência de nada |
| `MacroHelper.Tests` | xUnit — roda offline, em segundos |

Sete dependências no app inteiro: `Dapper`, `Microsoft.Data.Sqlite` e
`SQLitePCLRaw.bundle_e_sqlite3` para os dados, `CommunityToolkit.Mvvm` e
`Microsoft.Extensions.DependencyInjection` para a UI, `Microsoft.Web.WebView2` para a página
do Manual SQL e `System.Configuration.ConfigurationManager` para as preferências.

O `SQLitePCLRaw` é a única fixada à mão: o `Microsoft.Data.Sqlite` 8.0.11 arrasta a 2.1.6, e
a cópia do SQLite embutida nela tem o CVE-2025-6965. O porquê está no
`MacroHelper.Data.csproj`, ao lado da referência.

---

## Sobre esta cópia

Cópia pessoal e offline do SK MacroHelper, originalmente desenvolvido por
**Aline Martins · Silk**. Foi reduzida ao que uma pessoa usa sozinha: saíram equipe,
permissões, comunidade, agendamentos, IA, ditado por voz, API e app mobile, e o banco
hospedado deu lugar a um arquivo local. Caminhos desta máquina, branches e o remote
`upstream` em [CLAUDE.md](CLAUDE.md), que é o arquivo de desenvolvimento.
