# SKald: validação espacial executável

## 1. Resultado e alcance

**Há um percurso comprovado para o envelope de teste de 64 × 64 DIP, com 8 DIP de folga por lado, na região inferior de Notas.** Categorias também comporta esse percurso com uma categoria e sua subcategoria. As duas regiões comportam o teste nos tamanhos normal e mínimo, nos temas claro e escuro, com os dados sintéticos descritos aqui.

**Aprovado com ressalvas: diagnóstico WPF isolado.** Esta conclusão libera a calibração do lote visual L0/L1 para essa configuração. Não aprova a integração final, a animação de um personagem, o uso manual na janela visível ou outros tamanhos de conteúdo e escalas.

Foi executada somente a tarefa da **seção 11** de `SKald-plano-visual-e-producao-v1.md`, fornecido em Downloads. As outras seções serviram de referência. O briefing original, também em Downloads e anteriormente autorizado, e os quatro documentos SKald existentes foram preservados. O briefing continua ausente de `docs/`; nenhuma cópia foi criada silenciosamente.

Entregas locais:

- [Testes e renderizador executável](../MacroHelper.Tests/SkaldDiagnosticoEspacialTests.cs): matriz de telas, sequência temporal, input e interrupções.
- [Medição e camada diagnóstica](../MacroHelper.Tests/SkaldMapaEspacial.cs): referências WPF, recortes, candidatos, obstáculos e envelope.
- [Dados sintéticos e comandos contados](../MacroHelper.Tests/SkaldDadosDeDiagnostico.cs): contextos mutáveis em memória.
- [Índice completo de evidências](evidencias-skald-espacial/README.md): PNGs, medições JSON, sequência e resumo da execução.

Nenhum arquivo da aplicação foi alterado por esta etapa. Não houve integração do mascote, restauração de sprites, alteração de layout, dependências, banco pessoal, publicação, commit ou push.

## 2. Contexto, código e ambiente

| Item | Verificação |
| --- | --- |
| Checkout | `D:\MacroHelper-Local`, HEAD `634a562`, com alterações locais anteriores. A análise e o build usam essa árvore de trabalho, não somente o commit. |
| Documentação lida | `CLAUDE.md`, `README.md`, os quatro documentos SKald, briefing original e adendo visual. |
| Arquitetura | .NET 8 / WPF, MVVM, cinco projetos na solution. O diagnóstico fica apenas em `MacroHelper.Tests`. |
| Infraestrutura reutilizada | `TelaDeTeste`, thread STA com `Application` simples; `Xaml.RecursosDoApp(tema)`; `RelogioFalso`. |
| Banco sintético disponível | `BancoDeTeste` cria SQLite temporário; inspecionado, mas desnecessário ao diagnóstico, que usa coleções em memória. |
| SDK utilizado | .NET SDK `10.0.401`, compilando `net8.0-windows`; host de testes .NET `8.0.29`, x64. |
| Tamanho normal | `MainWindow.xaml`: `Width=1200`, `Height=760`. Árvore WPF medida e arranjada em 1200 × 760 DIP. |
| Mínimo | `MinWidth=940`, `MinHeight=600`, também lidos da instância `Window` carregada pelo XAML. Árvore medida e arranjada em 940 × 600 DIP. |
| Área CurrentView | Normal: `(263,66,924,681)`; mínimo: `(263,66,664,521)`. Tuplas são `x,y,largura,altura` em DIP da raiz comum. |
| Escala | Fonte WPF observada com `PixelsPerDip=1`; imagens renderizadas a 96 DPI. O monitor 1920 × 1080 é contexto fornecido pelo adendo, não uma captura medida nesta execução. |
| Temas | Recursos reais `Light.xaml` e `Dark.xaml`, carregados do assembly recompilado. |
| Evidência visual | PNGs do WPF real com legenda externa de 42 pixels. Assim, 1200 × 802 e 940 × 642 são tamanhos dos arquivos, não novos tamanhos da aplicação. |

### Isolamento e adaptações do hospedeiro

O harness lê o XAML atual de `MainWindow`, remove em memória `x:Class`, ícone e manipuladores de eventos da janela e exclui a instância oculta de `PaginaOcultaView`. Conserva o Grid, recursos, sidebar, painel e `ContentControl` de `CurrentView`. Instancia as quatro Views reais compiladas, com contextos sintéticos, e as coloca nesse mesmo hospedeiro.

A raiz fica ligada a um `HwndSource` **oculto, sem ativação e sem entrada na barra de tarefas**, para que WPF materialize templates, visibilidade e layout. Não chama `MainWindow` do produto, `App.OnStartup`, serviços de domínio, preferências, hooks, clipboard ou notificações. Não mostra nem fecha a janela pessoal do usuário. A `Window` base usada para ler o XAML nunca é exibida. `UseLayoutRounding`, idioma e opções de texto são preservados na raiz destacada.

A composição de uma fonte oculta não fornece a mesma cadência da janela visível. Por isso, o diagnóstico fixa o estado final da transição de entrada das Views: opacidade 1 e translação Y=0. **Não foi testada a fluidez desse storyboard.** Isso não altera os arquivos das Views.

O texto estático “Hook ativo” que aparece no XAML da sidebar é parte da apresentação original. **Não é evidência de um hook iniciado pelo ensaio.** Os contextos do diagnóstico não instanciam esse serviço.

### Dados utilizados

- Notas: 0, 1 ou 18 registros sintéticos, com título, resumo e data; a linha inteira continua sendo o Button real.
- Categorias: 0, 1 ou 18 raízes; cada raiz tem uma subcategoria. O cenário longo tem 36 linhas na hierarquia.
- Configurações: paleta real de 12 cores, textos e valores sintéticos. Os casos `01` e `18` variam contagens exibidas, não representam uma lista de configurações.
- Início: cenário curto com uma macro e uma tarefa; cenário longo com cinco macros no ranking, seis tarefas visíveis, 12 tarefas ocultas e 18 compromissos. Os limites de cinco e seis seguem o `DashboardViewModel` atual.
- Editor de nota, formulário de categoria e toast: textos explícitos de teste; nenhum conteúdo pessoal é lido.
- Relógio das trajetórias: controlado, partindo de 15/09/2026 às 10h. Os conversores de data existentes continuam usando suas próprias regras; o relógio falso não substitui o relógio de todo o produto.

## 3. Como reproduzir e o que foi executado

Comandos executados no repositório, com dependências locais já restauradas:

```powershell
dotnet build MacroHelper.sln --no-restore

$env:SKALD_EVIDENCIAS_DIR = 'D:\MacroHelper-Local\docs\evidencias-skald-espacial'
dotnet test MacroHelper.Tests/MacroHelper.Tests.csproj --no-restore --filter FullyQualifiedName~SkaldDiagnosticoEspacialTests --logger 'console;verbosity=minimal' --logger 'trx;LogFileName=skald-espacial.trx' --results-directory .\TestResults\skald-espacial

dotnet test MacroHelper.sln --no-build --no-restore --logger 'console;verbosity=minimal' --logger 'trx;LogFileName=regressao-final.trx' --results-directory .\TestResults\skald-espacial
```

Os comandos também tiveram execuções intermediárias com `--no-build` e filtros menores para corrigir o próprio harness. Elas não são aprovação do produto. Foram corrigidos: árvore sem fonte de apresentação, seleção acidental do ScrollViewer interno do TextBox, altura esticada do ItemsControl de Categorias, painel fora do viewport após scroll, ancestral de texto no hit test e referências de rolagem após navegação. A execução final é a fonte dos resultados no [resumo de execução](evidencias-skald-espacial/resumo-execucao.json).

Para repetir **preservando as evidências desta entrega**, escolha uma pasta nova antes de executar o filtro:

```powershell
$env:SKALD_EVIDENCIAS_DIR = Join-Path (Get-Location) ('docs\evidencias-skald-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
dotnet test MacroHelper.Tests/MacroHelper.Tests.csproj --no-restore --filter FullyQualifiedName~SkaldDiagnosticoEspacialTests
```

Sem essa variável, os testes continuam executando e gravam em uma pasta temporária exclusiva `MacroHelper-Skald/<guid>`. Não existe teste ignorado por ausência de variável. Cada rodada restaura os recursos WPF anteriores e libera o hospedeiro oculto. Não há ferramenta de navegador aplicável a essas Views WPF; não foi criada imitação HTML.

## 4. Método de medição e restrições

1. Localizar o `ContentControl` pelo binding `CurrentView` e o ScrollViewer da página pelo conteúdo real, excluindo os ScrollViewers internos de caixas de texto.
2. Medir `RenderSize` e converter os retângulos com `TransformToVisual` para a raiz comum. Intersectar com viewport, limites da raiz, `ClipToBounds` e clips dos ancestrais. Guardar retângulo original, visível e indicador de recorte nos JSONs.
3. Proteger Buttons, CheckBoxes, campos, seletores, ScrollBars, elementos com InputBindings, textos, estilos `ListRow` e `EmptyStatePanel`. A enumeração ocorre na medição, **não a cada quadro**.
4. Em Configurações, medir o espaço entre viewport e a borda esquerda da coluna central. `MaxWidth=760` é teto; a largura efetivamente renderizada da coluna nesta massa foi 560 DIP.
5. Em Notas/Categorias, usar o último container gerado da lista, incluindo subcategorias. O ItemsControl de Categorias pode esticar até o fundo do viewport mesmo com uma única raiz; sua altura total, isoladamente, não mede o fim dos registros. Proteger também o EmptyStatePanel e uma reserva inferior de 28 DIP.
6. No Início, buscar um retângulo livre **somente dentro do painel do cumprimento**. Não ligar esse bolsão aos outros cartões nem tratar toda a página como livre.
7. Reservar 12 DIP internamente ao viewport para evitar a periferia e os cantos do painel. Dentro do candidato, o corpo tem folga adicional de 8 DIP por lado: 64 + 16 = **80 DIP ocupados**.
8. Para o trajeto horizontal, verificar a união retangular entre início e pausa, expandida pela folga. Essa verificação cobre todo o segmento contínuo, além de 61 amostras de posição. O retorno usa o mesmo segmento.

Os clips destas telas são tratados por seus limites retangulares; o perímetro é conservadoramente excluído. Não há aprovação genérica de futuros clips arbitrários, rotações, sombras de sprites ou objetos fora do envelope. Qualquer alteração de tema, texto, fonte, lista, DPI ou layout exige nova medição.

## 5. Regiões medidas

Valores da massa curta (`01`), iguais entre os temas nesta rodada. `Máximo estático` é o maior quadrado geométrico após a folga. `Máximo com rota` também reserva deslocamento horizontal mínimo de 48 DIP. Esses máximos são limites geométricos calculados; o único corpo efetivamente ensaiado tem 64 DIP.

| Região | Janela | Candidato em DIP `(x,y,w,h)` | Máximo estático / com rota | Resultado para 64 + folga |
| --- | --- | --- | --- | --- |
| AN-CONFIG-LEFT | 1200 × 760 | `(275,78,162,657)` | 146 / 98 DIP | **Aprovado no ensaio**; percurso horizontal de 82 DIP dentro da lateral. |
| AN-CONFIG-LEFT | 940 × 600 | `(275,78,32,497)` | 16 / 0 DIP | **Rejeitado**; largura insuficiente, inclusive parado. |
| AN-NOTES-LOWER | 1200 × 760 | `(275,355,883,380)` | 364 / 364 DIP | **Aprovado no ensaio**; primeira região escolhida para R01. |
| AN-NOTES-LOWER | 940 × 600 | `(275,355,623,220)` | 204 / 204 DIP | **Aprovado no ensaio**. |
| AN-CATEGORIES-LOWER | 1200 × 760 | `(275,341,883,394)` | 378 / 378 DIP | **Aprovado no ensaio**; inclui a subcategoria no cálculo. |
| AN-CATEGORIES-LOWER | 940 × 600 | `(275,341,623,234)` | 218 / 218 DIP | **Aprovado no ensaio**. |
| AN-HOME-LOCAL | 1200 × 760 | `(507,100,460,61)` | 45 / 45 DIP | **Rejeitado**; o bolsão do cumprimento não tem altura. |
| AN-HOME-LOCAL | 940 × 600 | `(683,100,187,65)` | 49 / 49 DIP | **Rejeitado**; altura insuficiente. |

Essas coordenadas são **resultados**, não constantes colocadas no código. O teste calcula cada região a partir da árvore atual.

### Listas vazias, longas e rolagem

| Estado | Resultado obtido |
| --- | --- |
| Notas vazias | O painel de estado vazio ocupa espaço. Área inferior: 883 × 288 normal; 623 × 128 mínimo. O quadrado máximo com folga é 272 / 112 DIP, respectivamente; o envelope 64 cabe. |
| Categorias vazias | Área após o estado vazio: 883 × 279 normal; 623 × 119 mínimo. Máximo com folga 263 / 103 DIP; o envelope 64 cabe. |
| Notas com 18 registros | Área inferior rejeitada, tanto no topo quanto no final da rolagem; não sobra altura suficiente depois da última linha visível. |
| Categorias com 18 raízes e 18 filhas | Área inferior rejeitada no topo e após rolar ao fim. |
| Configurações roladas ao fim | A lateral continua sendo candidata no tamanho normal; permanece rejeitada no mínimo. A cena anterior é retirada antes de uma nova medição. |
| Início longo rolado ao fim | O painel do cumprimento sai do viewport. A região fica rejeitada, sem reaproveitar sua posição anterior. |
| Lista curta ganha 18 registros | O envelope é retirado; a nova medição rejeita a área inferior. |
| Toast aparece | O envelope é retirado e o layout é medido novamente; o índice contém os retângulos resultantes desse estado. |

**Não foi autorizado usar a sidebar, barra de título, botões, topo dos cartões ou espaços entre cartões como apoio.** Os ensaios acima aprovam áreas internas livres específicas e a trajetória descrita abaixo.

## 6. Percurso comprovado e ligação com C02/C03/C07/C08

Configuração da prova temporal: **Notas, um registro, tema escuro, 1200 × 760 DIP, 96 DPI**. Corpo 64 × 64, folga 8, sem sprites ou efeitos. A âncora usada no ensaio é o canto superior esquerdo do retângulo; a linha amarela marca sua base.

- Entrada local: corpo em `(283,663,64,64)`.
- Pausa: corpo em `(403,663,64,64)`.
- Retorno: mesmo segmento de 120 DIP, até a posição inicial, seguido de retirada.
- Envelope varrido incluindo folga: `(275,655,200,80)`, contido no candidato e sem interseção com os obstáculos.
- No mínimo, a mesma rota horizontal usa `y=503`, mantendo 8 DIP de folga inferior e as reservas do viewport.

Veja a [sequência de dez quadros e seus tempos](evidencias-skald-espacial/README.md#sequencia-temporal-r01) e o [manifesto temporal JSON](evidencias-skald-espacial/R01-sequencia.json).

| Tempo virtual | O que a evidência comprova |
| --- | --- |
| 0 s | Sem corpo. |
| 1 s | Entrada local já inteiramente dentro da região reservada. |
| 2 s | Deslocamento intermediário, x=323. |
| 4 s | Parada em x=403. |
| 6, 9 e 12 s | Ocupação estável da mesma área durante o intervalo reservado para abrir, ler e guardar papel. O retângulo não encena esses gestos. |
| 14 s | Retorno pelo mesmo caminho, x=323. |
| 15 s | Fim do deslocamento, x=283. |
| 16 s | Corpo removido. |

É uma **sequência de simulação com relógio controlado**, não vídeo em tempo real. A entrada é um aparecimento local seguido de deslocamento horizontal e a saída é retorno seguido de ocultação. **A entrada andando através da borda lateral, prevista no R01 completo, permanece pendente**: ela precisa do clipe e do tratamento explícito de revelação/oclusão. Não foi aprovada pela prova de um corpo sempre inteiramente visível.

| Referência | O que foi verificado | O que permanece futuro |
| --- | --- | --- |
| C02 | Posição inicial livre, chegada local e caminho curto. | Entrada artística pela borda, passada, aceno e saudação única da sessão. |
| C03 | Caminhar, reservar área de pausa e retornar sem invadir controles. | Abrir/ler/guardar pergaminho, virada, atuação e coerência dos pés. |
| C07 | Ponto sintético distante não retira; ponto dentro da proximidade de 64 DIP retira imediatamente. Busca alterada suspende e exige cinco segundos sem digitação para elegibilidade. | Receber ponteiro e digitação reais com a janela visível; ajustar distância após avaliação de uso. Nenhuma regra global foi vinculada a todo foco ou movimento do mouse. |
| C08 | Mudança real de `ContentControl.Content` invalida mapa e remove corpo; modal, resize, mensagem, lista e scroll também retiram. Versão antiga não pode reiniciar a cena. | Integração ao roteador real, bandeja, reabertura e ciclo completo da aplicação. |
| R01 | Primeira base espacial comprovada em Notas e alternativa em Categorias. | Coreografia completa de 12–20 s, objetos, assets, manifesto e reprodução no produto. |

### Envelope e família a produzir depois

Para **AN-NOTES-LOWER com uma nota**, o limite calculado é 364 DIP no normal e 204 DIP no mínimo, incluindo a exigência de folga e a rota de 48 DIP. Isso não recomenda ampliar o personagem: **manter 64 DIP como envelope inicial de corpo, capa e acessórios para L0/L1**, e validar os quadros reais antes de liberar a cena. Na lista vazia em tamanho mínimo, o limite já cai para 112 DIP; com lista longa, a região deixa de existir.

A próxima família é **corpo inteiro para exploração**, com orientação esquerda/direita coerente, âncora corporal estável e envelope de todos os quadros: parado, virar, andar, parar, recuar, olhar, acenar/despedir, abrir/ler/guardar papel e pensar. Usar L0 para modelo e L1 para o primeiro percurso, conforme o adendo. Este trabalho **não produziu** essas imagens nem alterou os contratos futuros de sprites.

## 7. Input e interrupções

**Resultado no harness:** o alvo retornado por `InputHitTest` foi o mesmo com e sem a camada para os pontos de controles visíveis dos 40 cenários de base. Inclui campos, botões e seletores, além dos Borders com MouseBinding de Configurações. Os JSONs registram `PontosDeInputVerificados`.

A camada tem `IsHitTestVisible=false`, `Focusable=false` e `IsEnabled=false`. A trajetória só altera o estado de desenho. Não chama `ICommand.Execute`, foco, eventos de clique, teclado ou mouse. Nos testes de interrupção, os contadores de eventos de mouse/teclado/foco ficam em zero durante o percurso e o elemento de foco permanece igual.

Há um controle positivo separado: o teste resolve o comando sintético pelo Button real e executa explicitamente esse comando **uma vez**, para provar que o contador funciona. O total permanece em um depois das interrupções e da navegação. Essa chamada pertence à calibração do teste, não à simulação do mascote. Os [arquivos de input](evidencias-skald-espacial/README.md#interrupcoes-e-input) separam os três momentos.

**Limite:** isso comprova passagem no hit testing WPF isolado e ausência de chamadas funcionais na simulação. Não mede atraso de clique real, foco do Windows, navegação por Tab, UI Automation, captura de mouse ou latência na aplicação visível. Os comandos usados são contados, sem funções de salvar, excluir, copiar ou navegar no domínio.

| Interrupção | Estímulo realmente executado | Resultado esperado e obtido |
| --- | --- | --- |
| Modal/editor | Alterar a propriedade ligada ao Grid real de sobreposição. | Grid visível; corpo e mapa retirados. |
| Rolagem | `ScrollToEnd()` no ScrollViewer real e evento `ScrollChanged`. | Cena retirada; mapa só volta após medição explícita. |
| Resize/restauração | Arranjar a raiz em 940 × 600 e depois 1200 × 760; evento `SizeChanged`. | Retirada, novas dimensões e novo mapa. Não equivale a maximizar pelo Windows. |
| Coleção cresce | Adicionar 18 objetos à ObservableCollection vinculada. | Retirada, lista materializada e região inferior rejeitada. |
| Mensagem | Exibir toast sintético pela propriedade `Mensagem`. | Retirada e nova geometria. |
| Navegação | Trocar o conteúdo do hospedeiro por `DashboardView` real. | Camada sem corpo nem mapa do contexto anterior. |
| Proximidade | Dois pontos sintéticos, distante e próximo do corpo. | Distante permite continuar; próximo retira. |
| Digitação/contexto antigo | Alterar busca e avançar `RelogioFalso`. | Aguarda estabilidade e cinco segundos sem digitação; versão antiga rejeitada e sem retomada automática. |

Nos JSONs de retirada, `MapaAtivo=false` e `Situacao=suspenso-mapa-invalidado` indicam que os retângulos armazenados são a última medição, **não uma aprovação do modal ou da tela seguinte**. As sobreposições visíveis são registradas separadamente. O recálculo é explícito no harness; não foi implementado um diretor autônomo no produto.

## 8. Cobertura e pendências

| Grupo | Status | Evidência / limite |
| --- | --- | --- |
| Quatro Views, dois tamanhos, dois temas, massa curta/longa | **Aprovado no diagnóstico** | 32 combinações de base. Aprovar o teste de diagnóstico inclui concluir corretamente que algumas regiões não cabem. |
| Notas e Categorias vazias | **Aprovado no diagnóstico** | Oito combinações adicionais; 40 de base no total. |
| Scroll de Configurações e massas longas | **Aprovado no diagnóstico** | 20 estados após rolagem, com medição e PNG. |
| Editor/formulário, mensagem, lista e navegação | **Aprovado no diagnóstico** | Quatro sequências: Notas normal/claro e mínimo/escuro; Categorias normal/escuro e mínimo/claro. As outras combinações de modal/tema/tamanho não foram executadas. |
| Resize, proximidade e busca | **Aprovado no diagnóstico** | Sequência própria em Notas/escuro, normal → mínimo → normal. |
| Trajetória contínua do envelope | **Aprovado no diagnóstico** | União dos envelopes e 61 posições por rota; dez quadros temporais de R01. |
| Janela pessoal visível, ativação e uso simultâneo | **Não executado** | A ferramenta de controle nativo não está disponível nesta sessão; a prova usa fonte WPF oculta. |
| Mínimo imposto pelo arraste do Windows, maximização e múltiplos monitores | **Não executado** | Foram confirmadas as propriedades da Window e o layout nos tamanhos indicados, sem manipulação nativa da janela. |
| 125%, 150%, 200%, troca de DPI e fontes alternativas | **Não executado** | Somente árvore com escala 1 e renderização a 96 DPI. |
| Dados pessoais, banco real e resultado de consultas/serviços | **Não executado** | Fixtures em memória. Não houve acesso ao banco ou às preferências pessoais. |
| Manual SQL, WebView2, janelas auxiliares e demais telas | **Fora deste ensaio** | Nenhuma mudança de dependência ou tentativa de sobreposição nesses hospedeiros. |
| Saltos, corrida, sentar, deitar, efeitos, sons e assets | **Não implementado** | Apenas retângulo de teste e rota horizontal. |
| Desempenho do produto, CPU/FPS, clique sem atraso | **Não executado** | Duração da suíte não é benchmark de animação ou da aplicação. |

### Validação manual posterior

Em uma futura integração isolada ou instalação de teste, usando massa sintética:

1. Conferir a região no tamanho normal e no mínimo real pelo arraste, em cada tema e DPI.
2. Digitar na busca, usar Tab, clicar nos botões e rolar durante uma cena; comparar comportamento e tempo de resposta com o mascote oculto.
3. Abrir editor/formulário e navegar durante a pausa; verificar remoção de corpo, papel, efeitos e relógios juntos.
4. Aumentar listas, mudar resolução/monitor, maximizar/restaurar e conferir recálculo ou retirada, sem encolher automaticamente a arte.
5. Conferir o futuro L0/L1 em tamanho de uso, incluindo envelopes de capa, mãos e papel, contato dos pés e a entrada/saída lateral completa.

## 9. Relação com os documentos anteriores

Os quatro documentos anteriores continuam sendo o snapshot de mapeamento e planejamento. As afirmações de que não existiam harness, renderização ou medições pertencem àquela etapa; **este relatório acrescenta a prova isolada**. As cenas continuam planejadas, sem mascote integrado ao produto.

A largura máxima declarada em Configurações não garantia uma lateral livre constante. A execução confirmou uma lateral viável no normal e inviável no mínimo. A altura esticada do ItemsControl de Categorias também não correspondia ao fim dos registros. Ambas as decisões agora têm referências de elementos e medições reproduzíveis.

A orientação atual do adendo sobre foco prevalece para este diagnóstico: foco parado ou ponteiro distante não são, sozinhos, motivo para suspensão global. O ensaio de proximidade e alteração da busca não implementa todo o sistema futuro de elegibilidade.

As instruções de publicação automática presentes em `CLAUDE.md` não se aplicam: a solicitação atual e a seção 11 limitam explicitamente esta entrega a diagnóstico local, sem publicação.

## 10. Resultado final

**Execução final aprovada: build com zero avisos e zero erros; 499 testes aprovados, incluindo os 46 do diagnóstico, sem falhas ou testes ignorados.** Foram gerados e decodificados 96 PNGs. Os 190 arquivos preexistentes inventariados no início foram conferidos por SHA-256 e permaneceram iguais.

**Critério espacial atendido no harness:** existe uma rota sem colisão para o envelope 64 + 8 DIP em configuração documentada. A primeira região indicada é **AN-NOTES-LOWER com lista curta**; Categorias é alternativa; Configurações depende de largura suficiente; o bolsão do cumprimento do Início foi rejeitado para esse envelope.

Consulte o [resumo verificável da execução final](evidencias-skald-espacial/resumo-execucao.json) e o [manifesto das fontes](evidencias-skald-espacial/fontes-verificadas.json) para resultados e integridade. O funcionamento da aplicação foi preservado por isolamento e pela suíte local; a aprovação manual do futuro mascote permanece pendente.
