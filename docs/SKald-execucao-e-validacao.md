# SKald: execução, inspeção e plano de validação

**Situação em 15/09/2026: planejamento documental.** O ambiente possui SDK e runtime adequados ao alvo do projeto. As telas e os estados foram identificados no código; nenhuma tela foi aprovada por inspeção visual nesta etapa. Não foram executados build, suíte de testes, inicialização do aplicativo, publicação ou operações com dados pessoais.

## 1. Referências e alcance

- Código da cópia de trabalho em `D:\MacroHelper-Local`, incluindo alterações locais já existentes.
- `CLAUDE.md`, `README.md`, projetos, manifesto do Windows, inicialização, views, controles, ViewModels e infraestrutura de testes.
- `C:\Users\Raul Janon\Downloads\SKald-briefing-completo.md`, autorizado pelo usuário como referência porque `docs/SKald-briefing-completo.md` não existia nesta cópia no início da análise. Os itens 17, 18 e 26 a 33 orientam especialmente os critérios abaixo.
- As instruções dos documentos são contexto para planejamento. Não constituem autorização para publicar, iniciar serviços, produzir sprites ou implementar a integração nesta etapa.

**Termos usados:** “revisão estática” significa leitura de código/configuração. “Runtime” significa o aplicativo efetivamente em execução. Encontrar um estado no XAML não prova que ele se apresenta corretamente na tela.

## 2. Como executar o projeto

### 2.1 Tecnologia e ambiente confirmados

| Item | Evidência atual |
| --- | --- |
| Tipo de aplicação | Desktop Windows, `WinExe`, WPF e Windows Forms; alvo `net8.0-windows`, RID `win-x64`. `MacroHelper.UI/MacroHelper.UI.csproj:3-8,23`. |
| SDK selecionado pelo terminal | `dotnet --version` retornou `10.0.401`, com saída de sucesso. |
| SDK do alvo instalado | `dotnet --list-sdks` incluiu `8.0.423`. Também há SDKs 2.1, 5.0 e 10.0. |
| Runtime desktop do alvo instalado | `dotnet --info` incluiu `Microsoft.WindowsDesktop.App 8.0.29`. |
| Fixação do SDK | `dotnet --info` informou ausência de `global.json`. A seleção local atual é 10.0.401, enquanto o CI configura `8.0.x`. Isso é uma diferença de ambiente, não uma falha de build demonstrada. |
| Host local | Windows, RID `win-x64`, segundo `dotnet --info`. |
| Dependências | CommunityToolkit.Mvvm, DI, ConfigurationManager, WebView2 e os projetos Core/Data/Services, conforme o csproj. Nenhuma dependência foi instalada. |
| Node | `node.exe` e `npm.ps1` estão no PATH. Isso não transforma o aplicativo em projeto web nem comprova uma instalação de Playwright. |
| Arquivos web/automação | Não foram encontrados `package.json`, configuração Playwright ou servidor de desenvolvimento web na árvore examinada. Há o HTML do Manual SQL. |
| Renderizador WPF para PNG | O `CLAUDE.md:220-245` descreve um utilitário temporário de sessão. Não foi encontrado um renderizador equivalente versionado no projeto. |

### 2.2 Comandos existentes, não executados nesta etapa

Executar na raiz do repositório, em Windows. Estes são comandos de desenvolvimento documentados, apresentados para uma etapa posterior:

```powershell
dotnet build MacroHelper.sln
dotnet test MacroHelper.sln
dotnet run --project MacroHelper.UI
```

Fonte: `CLAUDE.md:57-69`. O CI usa outra sequência, em Release:

```powershell
dotnet restore MacroHelper.sln
dotnet build MacroHelper.sln -c Release --no-restore
dotnet test MacroHelper.sln -c Release --no-build --verbosity normal
```

Fonte: `.github/workflows/ci.yml:21-33`. Não foi alterado nem acionado o pipeline. `Directory.Build.props:14-26` trata warnings como erros, habilita nullable e análise de estilo, e solicita build determinístico.

Se as dependências já estiverem restauradas, `dotnet build MacroHelper.sln --no-restore` é um primeiro check local possível. A presença do SDK não garante que todos os pacotes e artefatos de restore estejam disponíveis. O comando sem restore ainda grava saídas de compilação; não é uma inspeção somente de leitura.

### 2.3 Por que iniciar o app não é uma prévia neutra

O fluxo normal cria ou reutiliza o contexto local, aplica migrations, configura preferências e mostra a janela. Ao carregar `MainWindow`, inicia hook de teclado, bandeja, atalho global, monitor de clipboard e lembretes.

Evidências: `MacroHelper.UI/App.xaml.cs:73-137,178-232`; `MacroHelper.UI/Views/MainWindow.xaml.cs:84-125,280-366`; `MacroHelper.Data/Context/SqliteContext.cs`.

- O banco padrão fica no perfil do usuário; abrir a aplicação pode aplicar migrations nele.
- A instância única usa o mutex `MacroHelper_SK_SingleInstance`; uma segunda execução pode interagir com a instância já aberta. Não se deve encerrá-la à força para obter uma captura.
- Monitoramento de clipboard e hooks atingem a sessão do Windows, além da janela em revisão.
- A página pessoal utiliza WebView2 e estado local. Sua verificação não exige ler perfil, autenticar nem navegar para o serviço externo.

Por isso, a inspeção desta etapa ficou na documentação e no código. Não faltou SDK: faltam uma sessão controlada para os testes visuais e um mecanismo de inspeção nativa habilitado. Não foi criado um modo de demonstração, um banco alternativo ou um renderizador.

## 3. O que pode ser inspecionado no navegador

### 3.1 Aplicativo WPF

Não há URL de desenvolvimento, rotas HTTP ou DOM para `MainWindow`, Macros, Notas ou outras telas WPF. A navegação troca `CurrentView` e `PaginaAtiva` em `MainViewModel`, exibidos por `ContentControl`; abrir um navegador não renderiza esses controles.

As ferramentas de navegador da sessão podem inspecionar páginas web. As APIs de controle nativo de computador estão desabilitadas nesta sessão. Portanto, elas não disponibilizam inspeção de foco, mouse, árvore visual ou janelas WPF por meio da automação de navegador. Não foi aberto o navegador pessoal para contornar essa limitação.

### 3.2 Exceção: Manual SQL

`MacroHelper.UI/Resources/manual_sql_max.html` pode ter seu conteúdo HTML analisado separadamente em um navegador, por abertura de arquivo ou por um servidor local de prévia preparado posteriormente. O projeto não fornece um servidor de prévia nem um comando web próprio.

No aplicativo, `ManualSqlView` carrega esse arquivo com `WebView.Source`; se o arquivo não existir, oculta o WebView e exibe um aviso com o caminho esperado (`ManualSqlView.xaml.cs:14-31`). O csproj o copia para a saída (`MacroHelper.UI.csproj:37-39`).

Uma futura prévia HTML permite verificar busca, expansão e disposição do manual. Ela não confirma:

- dimensões, bordas, transparência, tema e espaço disponível no host WPF;
- comportamento do WebView2 embutido ou sobreposição do mascote;
- navegação da aplicação, atalhos globais, janelas flutuantes ou modais WPF;
- execução de consultas SQL. O manual é conteúdo de referência, e a análise não solicita executar os scripts exibidos.

O HTML contém função de cópia para clipboard (`manual_sql_max.html:1259`). Em uma inspeção de layout, não é necessário acionar essa função. Nenhum teste de navegador foi executado nesta etapa.

### 3.3 Limite específico do WebView2

O `CLAUDE.md:193-195` relata funcionamento em janela transparente nesta máquina e generaliza que o problema de sobreposição não se aplica. Isso é um relato histórico, insuficiente para aprovar uma camada WPF sobre conteúdo web.

O controle WPF padrão `WebView2` herda de `HwndHost` e tem a limitação conhecida de sobreposição de conteúdo nativo, chamada *airspace*. A Microsoft documenta `WebView2CompositionControl` como alternativa para esse problema. O projeto atual usa `WebView2`; substituir o controle exigiria análise própria e não faz parte desta etapa. [Documentação oficial da Microsoft](https://learn.microsoft.com/en-us/microsoft-edge/webview2/platforms/wpf).

**Planejamento conservador:** tratar o retângulo web inteiro como região protegida, permitindo apenas locais WPF externos com espaço comprovado. Uma prévia HTML e uma captura do XAML não encerram essa pendência.

## 4. Recursos locais de testes e renderização

### 4.1 Infraestrutura existente

| Recurso | O que permite verificar | Limite |
| --- | --- | --- |
| `BancoDeTeste.cs:20-27,60-70` | SQLite real temporário por teste, com GUID, migrations e remoção dos próprios temporários. | Não representa o banco pessoal nem prova comportamento em todos os conjuntos de dados. |
| `RelogioFalso`, em `BancoDeTeste.cs:75-79` | Reproduzir prazos e passagem do tempo sem esperar pelo relógio real. | Não existe ainda um relógio/controlador de cenas do SKald. |
| `TelaDeTeste.cs:20-59` | Thread STA única, recursos XAML e execução serial de ações WPF. Instancia `System.Windows.Application`, sem iniciar `App.OnStartup`. | Não é ferramenta de captura visual nem simula uma sessão completa do Windows. |
| `Xaml.cs:56-85` | Carregar recursos do app e temas por pack URI no contexto de teste. | Uma imagem gerada com assembly antigo pode não refletir o XAML atual. |
| `XamlTests.cs` | Consistência de recursos, bindings e convenções cobertas pelos testes. | Não mede obstrução, salto, desenho dos sprites, fluidez ou foco real. |
| Testes de Notas, Compromissos, janelas flutuantes e buscador | Exemplos existentes de montagem de telas e dados controlados. | A presença dos testes é evidência de infraestrutura, não de execução nesta análise. |
| `ParalelismoDesligado.cs:1-7` | Evita concorrência entre limpeza de pools SQLite e os demais testes. | Não valida concorrência entre animações e eventos da interface. |

A inspeção dos fixtures indica isolamento dos bancos de teste. A entrega altera somente Markdown; build e testes não foram executados nesta etapa. Em uma futura implementação, usar a suíte existente e acrescentar testes que validem os riscos novos do controlador e do mapa espacial.

### 4.2 Estratégia futura de prévia sem iniciar a aplicação

O `CLAUDE.md:220-245` descreve um caminho aproveitável: carregar o XAML com recursos do app, fornecer dados fictícios mutáveis, medir e organizar o layout, processar o Dispatcher e gerar PNG com `RenderTargetBitmap`.

Para tornar a prévia reproduzível numa próxima etapa:

1. Criar um host de inspeção isolado, sem instanciar `App` ou os serviços de sessão do Windows.
2. Reutilizar recursos e temas atuais. Registrar versão/commit e as alterações locais presentes, tamanho da janela, DPI e dados fictícios usados.
3. Preparar casos vazios, poucos registros, muitos registros, textos longos, erros e formulários abertos. Os fixtures devem representar estados existentes, sem mascarar falhas.
4. Medir o layout real e produzir capturas em tamanho de uso, incluindo o envelope inteiro dos gestos, objetos e efeitos.
5. Identificar claramente componentes não renderizados, handlers removidos e substituições de dependências. Remover handlers de um XAML de prévia significa que seu comportamento não está sendo exercitado.
6. Validar depois em sessão WPF real controlada: foco, digitação, rolagem, menus, ordem de janelas, DPI, WebView2 e interrupções não são aprovados por PNG.

Essa estratégia é proposta. O utilitário, os dados e as imagens não foram produzidos nesta etapa.

## 5. Matriz de cobertura da análise

**Legenda:** E = revisado estaticamente no código; V = verificação visual/runtime. Todas as linhas abaixo têm **V não executado**. As condições são estados encontrados ou cenários necessários para sua verificação, e não resultados observados em execução.

Motivos: **F** = ferramenta nativa desabilitada; **D** = dados/sessão controlados ainda não preparados; **W** = precisa do host WebView2 real; **A** = sprites e integração ainda não produzidos. F afeta toda a inspeção visual WPF, D afeta os estados dependentes de dados, e A afeta toda aprovação de cenas.

| Superfície | Cobertura E: código/estados identificados | Pendência V e motivo |
| --- | --- | --- |
| Janela principal e navegação | `MainWindow`/`MainViewModel`: seleção de página, grupo Macros expandido/recolhido, menu de avatar, conteúdo central, minimizar/restaurar e bandeja. | Geometria, foco, recortes e suspensão do mascote; F/A. |
| Dashboard | `DashboardView`: saudação, indicadores, mais usadas vazio/preenchido, compromissos de hoje e tarefas de hoje/atrasadas. | Densidade, títulos longos, áreas livres e atualização de cards; F/D/A. |
| Macros | `MacrosView`/`MacrosViewModel`: busca, filtros, carregamento, lista vazia/preenchida, favorito, ativo/inativo, mensagens. | Distinguir vazio após carga de filtro sem resultado; garantir leitura e clique nas linhas; F/D/A. |
| Formulário de macro | Overlay do `MacrosView` e `MacroFormViewModel`: criação/edição, variáveis, opções e erro. | Foco, validação e ocultação imediata da exploração; F/D/A. |
| Categorias | `CategoriasView`: lista vazia, hierarquia, formulário, erro e confirmação de exclusão com indicação de uso. | Expansão, atualização de apoios e proteção de confirmações; F/D/A. |
| Variáveis globais | `VariaveisGlobaisView`: registros, formulário e confirmação de exclusão com usos. | Textos longos, erro, atualização da lista e controles protegidos; F/D/A. |
| Tarefas | `TarefasView`/`TarefasViewModel`: listas filtradas, pendência/conclusão, prazos, prioridade, lembrete, recorrência, edição. | Vazio, muitos itens, mudança de posição após ação e alerta durante edição; F/D/A. |
| Compromissos em lista | `CompromissosView`/`CompromissosViewModel`: agendado, realizado, cancelado, com/sem data, filtros e formulário. | Mensagens, densidade, mudanças de status e ausência de espaço; F/D/A. |
| Agenda semanal/mensal | `AgendaCalendarioView` e `PainelDeAgenda`: semana/mês, dia atual, itens sem data fora da grade e compromissos. | Colisões de horários, rolagem, troca de período, células densas e recortes; F/D/A. |
| Notas em lista | `NotasView`: busca, coleção vazia/preenchida, fixada/desfixada, atualização e confirmação de exclusão. | Separar “não há notas” de “busca sem resultado” e “carga não concluída”; F/D/A. |
| Editor de notas | Overlay `MostrarEditor`, `EditorVisualizando`, editor formatado, prévia e `EditorErro`. | Seleção de texto, barra contextual, foco, atalhos e mensagem sem obstrução; F/D/A. |
| Buscador rápido | `BuscadorRapidoWindow`: cinco abas, busca, resultados heterogêneos, seleção, vazio, prévia e ações contextuais. | Troca rápida de abas, teclado, tooltip, resultado alterado e retorno ao app de origem; F/D/A. |
| Aba Copiados | Template de clipboard no buscador: conteúdo, origem, fixação, criação e limpeza. | Inspeção com conteúdo fictício; nenhuma leitura do clipboard pessoal; F/D/A. |
| Nota flutuante | `NotaFlutuanteWindow`: nova/edição, formatação, prévia, salvar, apagar e inserir. | Janela ativa única, teclado e foco sem instância duplicada do mascote; F/D/A. |
| Tarefa flutuante | `TarefaFlutuanteWindow`: prioridade, prazo, observações, recorrência e campos relacionados. | Formulário denso, listas abertas e redimensionamento por conteúdo; F/D/A. |
| Compromisso flutuante | `CompromissoFlutuanteWindow`: data/hora, estado e aviso antecipado. | Campos opcionais, combinações inválidas e tratamento de ausência de data; F/D/A. |
| Preenchimento de variáveis | `VariaveisWindow`: campos gerados, prévia, cancelar e inserir. | Tamanhos variáveis, obrigatoriedade, cancelamento e foco; F/D/A. |
| Popup de macros | `MacroPopupWindow`: sugestões, seleção por teclado, inserir e fechar. | Preservar campo original, posição do popup e ausência do mascote fora do app; F/D/A. |
| Tour | `TourWindow`: conteúdo por etapa, próximo e pular. | Avanço/fechamento sem prender foco nem exigir espera da animação; F/A. |
| Configurações | `ConfiguracoesView`: tema, paleta, dados de uso/saúde, comportamento, captura de atalhos e mensagens. | Mudança de tema em cena, captura de teclado e persistência; F/D/A. |
| Ajuda | `AjudaView`: perguntas recolhidas/abertas, atalhos e ação externa. O rótulo “Buscar na ajuda” é apresentação estática, sem campo de pesquisa funcional ligado. | Expansão, texto extenso e reposicionamento de apoios; F/A. |
| Manual SQL | `ManualSqlView`/HTML: arquivo presente/ausente, conteúdo web local e estado de erro do host. | HTML não aberto; host WPF/WebView2 e sobreposição não observados; F/W/A. |
| Página pessoal | `PaginaOcultaView`: criação tardia, abrindo, navegação embutida, ocultação e mensagem de indisponibilidade. | Serviço/conta externa não acessados; área web protegida, sem explorar conteúdo; F/W/A. |
| Componentes e interrupções | `SeletorDeData`, `BarraDeMarcacao`, `LinhaDeEstado`, menus, tooltips, listas abertas, confirmações e diálogos nativos acionados pelos fluxos. | Transições simultâneas e ordem de foco/janelas; F/D/A. |

Não foram coletadas capturas de tela, vídeos, logs de runtime, dados de banco pessoal ou evidências de sprites. A existência de uma região candidata no catálogo de cenas permanece condicionada à validação visual do espaço real.

## 6. Roteiro de QA para a futura integração

### 6.1 Dados e ambiente de teste

Usar dados fictícios reconhecíveis, como “Macro de teste 01”, notas curtas/longas e compromissos de teste. Preparar vazio, um registro, múltiplos registros, paginação/rolagem quando houver, busca sem resultado, filtros, registros ativos/inativos e estados concluídos/cancelados aplicáveis. Incluir retornos tardios e falhas simuladas na dependência correspondente, sem modificar mensagens para tornar o teste positivo.

A janela parte de 1200 × 760, com mínimo de 940 × 600 (`MainWindow.xaml:7-8`). São dimensões WPF de layout; não devem ser usadas como pixels físicos fixos em qualquer monitor. O manifesto declara PerMonitorV2 (`app.manifest:6-7`).

Matriz visual proposta: tamanho padrão/mínimo/maximizado; temas claro/escuro e cores de destaque representativas; escalas 100%, 125%, 150% e 200%; um monitor e dois monitores com DPI diferente. Esses valores são cobertura planejada, não ambientes já verificados.

### 6.2 Cenários de aceitação

| ID | Exercício futuro | Resultado esperado |
| --- | --- | --- |
| QA-01 | Reproduzir caminhar, correr, ler, pensar, saltar, sentar, deitar e levantar em cada região candidata. | Corpo, capa, pergaminho e efeitos cabem em todo o percurso, preservando textos e controles. Sem espaço, a cena é inelegível. |
| QA-02 | Fazer SKald examinar ou tocar a borda de botão permitido, com instrumentação do comando. | Zero comandos, cliques sintéticos, mudanças de foco ou alterações funcionais causadas pelo mascote. O botão mantém geometria e rótulo. |
| QA-03 | Aproximar o ponteiro e alcançar o apoio por Tab durante repouso e salto. | Uso do controle tem prioridade; a área é liberada sem esperar a animação e sem capturar eventos. |
| QA-04 | Digitar, selecionar, usar atalhos e rolar no editor enquanto há cena ao redor. | Texto e seleção preservados; sem roubo de foco, inserção acidental ou efeitos sobre o campo. |
| QA-05 | Abrir menu, tooltip, calendário, lista suspensa, barra contextual, overlay de edição e diálogo nativo. | Obstáculo temporário invalida a região; modal suspende/oculta exploração. Sem desenho sobre a tarefa do usuário. |
| QA-06 | Filtrar, excluir/alterar um registro de teste, recolher seção, rolar ou trocar página durante uma cena. | Apoio desaparecido cancela o trajeto; retirada segura ou ocultação de contingência, sem atravessar conteúdo. |
| QA-07 | Redimensionar e trocar de monitor durante caminhada, salto e pose deitada. | Coordenadas recalculadas no referencial correto; nenhum salto de escala, corte ou extrapolação da janela. |
| QA-08 | Abrir buscador/janela flutuante, minimizar para bandeja, ocultar e restaurar. | Uma única instância visível; nenhum pet sobre outros programas; pausa sem fila acumulada e contexto revalidado na volta. |
| QA-09 | Disparar sucesso e erro reais em sequência, duplicar evento e concluir operação de uma tela já fechada. | Resultado associado à operação correta; evento obsoleto descartado, sem comemoração duplicada ou tardia. Mensagem funcional imediata. |
| QA-10 | Iniciar processamento curto/longo, cancelar quando o fluxo permitir, emitir alerta durante cena decorativa. | Prioridade funcional, transição em ponto desenhado quando possível e nenhuma informação falsa de sucesso/correção. |
| QA-11 | Alternar pausar, ocultar e movimento reduzido em qualquer etapa. | Interromper planejamento e movimento conforme preferência, sem fila invisível; informação funcional permanece nos controles reais. |
| QA-12 | Alternar tema e destaque durante cenas e conferir foco/leitura com teclado. | Contraste e identidade preservados; sem redesenhar texto de interface no sprite. Elemento decorativo não duplica leitura acessível. |
| QA-13 | Fornecer pacote de teste com quadro ausente, arquivo corrompido, manifesto inválido e sequência incompatível. | Falha controlada do recurso decorativo, pose válida ou ocultação; app e operações continuam, com diagnóstico limitado ao problema. |
| QA-14 | Repetir entrada/saída de telas e carga/descarga do componente; observar ociosidade prolongada. | Sem instâncias, timers ou assinaturas duplicadas; fila limitada; nenhum planejamento contínuo quando oculto. |
| QA-15 | Medir fluidez, uso de memória e CPU com e sem mascote, usando o mesmo cenário. | Sem degradação perceptível de digitação/rolagem; custo medido e limites definidos por baseline real antes de aprovar. Não há meta numérica já validada. |
| QA-16 | Revisar a sequência completa e suas saídas antecipadas na escala de uso. | Passadas combinam com distância; objetos são recolhidos; deitar/levantar usam poses próprias; sprites e deslocamento não reiniciam por atualização de layout. |
| QA-17 | Testar Manual SQL com host web presente e arquivo ausente, sem executar SQL. | Mascote restrito às regiões WPF aprovadas; erro legível; ausência de sobreposição indevida do WebView2. |
| QA-18 | Salvar preferências do mascote quando forem implementadas, fechar/reabrir na sessão de teste. | Persistência correta sem alterar dados existentes; nenhum ressurgimento inesperado após escolher ocultar. |

### 6.3 Evidências mínimas para registrar a execução

Para cada caso, registrar objetivo, pré-condições, ambiente, dados fictícios, ID da cena/região, ação do usuário, resultado esperado/obtido, captura ou vídeo quando pertinente e pendências. Para “não acionou botão”, usar contagem de chamadas no comando/serviço de teste; só observar o desenho não comprova ausência de efeitos.

Classificação do caso: **Aprovado**, **Reprovado**, **Bloqueado**, **Não executado** ou **Aprovado com ressalvas**, conforme evidência real. Gravar motivos concretos para os itens bloqueados. Não confundir “encontrado no código”, “build passou”, “teste automatizado passou” e “cena aprovada visualmente”.

## 7. Resultado desta etapa

- **Executado:** inventário estático de código/configuração e comandos de consulta do SDK/runtime/ferramentas no PATH.
- **Produzido:** documentação de execução, limites de inspeção, matriz de cobertura e plano de QA.
- **Não executado:** build, testes automatizados, prévia HTML, render WPF, app real e qualquer operação externa ou com dados pessoais.
- **Pendente para implementação:** host de cena, mapa espacial, controlador, preferências e sprites aprovados; depois, medidas e testes com dados controlados.
- **Conclusão:** o projeto pode ser desenvolvido localmente em Windows. A integração deve ser verificada como WPF; o navegador atende apenas o conteúdo HTML separado. O catálogo documenta candidatos e critérios, não aprova trajetórias antes de renderização e uso real.
