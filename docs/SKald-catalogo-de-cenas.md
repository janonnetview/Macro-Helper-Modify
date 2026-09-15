# SKald: catálogo de cenas para integração

> Planejamento em 15/09/2026. Nenhuma cena, sprite ou integração descrita neste documento foi implementada ou validada em execução nesta etapa.

## Escopo, fonte e leitura

A referência autorizada pelo usuário é C:\Users\Raul Janon\Downloads\SKald-briefing-completo.md. O caminho docs/SKald-briefing-completo.md não estava presente nesta cópia durante o levantamento. O briefing foi lido como referência de produto e direção de arte; suas instruções de produção não autorizam implementação nesta etapa. Consulte também [o mapa do sistema](SKald-mapa-do-sistema.md), que registra arquitetura, navegação, evidências e pendências de inspeção.

Este catálogo contém **60 cenas propostas**, identificadas de C01 a C60. Elementos citados foram identificados por leitura de XAML, ViewModels e code-behind. Os espaços descritos são **candidatos condicionais**, não coordenadas aprovadas: não houve observação das cenas funcionando, medição visual em runtime nem produção dos sprites. A ausência de espaço elimina uma cena do sorteio; não autoriza deslocar controles ou reduzir o personagem até ficar ilegível.

O catálogo de 120 contextos e o núcleo de 24 sequências de mesa/444 posições de quadro do briefing são propostas de produção, não assets existentes. A locomoção exige família própria de corpo inteiro. O aplicativo atual é WPF para Windows; as menções a DOM e CSS no briefing deverão ser adaptadas à composição e à geometria do aplicativo.

## 1. Contrato obrigatório para todas as fichas

### Elegibilidade e semântica

- **G:** janela principal visível e ativa, tela estável após seu carregamento e transição, preferências futuras do mascote habilitadas, mapa atualizado, caminho e saída disponíveis, ausência de modal/formulário/popup/menu em uso. Para cenas inferidas de uma lista ou contagem, G exige que a consulta atual tenha terminado com sucesso; não vale durante loading nem após falha dessa consulta. Isso não proíbe C24: a falha confirmada de uma operação é seu próprio gatilho, desde que os requisitos de espaço e uso continuem atendidos.
- **D:** cena decorativa. Usar intervalo inicial de 45–90 segundos após a conclusão da última cena, somente com ociosidade suficiente. Não representa gravação, pesquisa, processamento, sucesso ou erro. Ajustar pesos para evitar repetir cena, apoio ou direção; propostas de frequência dependem de teste.
- **R:** reação a evento real. Exige resultado da operação correlacionado ao contexto atual, e não apenas clique, texto de mensagem, coleção vazia ou término de animação. Os ViewModels atuais têm retornos, propriedades e mensagens, mas não oferecem um contrato unificado de eventos para SKald. Esse adaptador ainda precisa ser projetado e implementado.
- **I:** retirada ou suspensão provocada pelo uso da interface. Tem prioridade sobre D e R; não espera o mascote terminar para permitir ação do usuário.
- **P:** presença em vinheta de mesa ou retrato. Exige uma área futura explicitamente reservada; não existe slot pronto para SKald no layout atual. Sem esse espaço, manter oculto.
- Qualquer leitura de pergaminho usa grafismos fictícios do personagem. Não copiar, analisar nem registrar títulos, textos de notas, valores de variáveis, conteúdo de clipboard ou dados do Manual SQL.
- Uma reação expira se o usuário navegar, abrir editor ou trocar de contexto antes de existir local livre. Não reproduzir depois uma fila de resultados antigos. Cancelamento não é erro e envio de texto a outro programa não confirma conclusão de trabalho nesse programa.

### Geometria, passagem e apoios

Todos os destinos ficam dentro da área cliente do aplicativo. A borda da janela é um limite, não a área de trabalho do Windows. Usar o mesmo referencial local da camada decorativa e medidas em unidades de layout WPF. A família de corpo inteiro parte da proposta de 64 × 64 pixels lógicos, exportada em 128 × 128; o tamanho real será calibrado depois da arte, incluindo escala/DPI.

Validar o envelope de **todos os quadros** de cada movimento: corpo, capa, mão, pergaminho, botas, sombra e efeitos. O espaço para deitar pode ser mais largo que o usado para ficar em pé. Saltos exigem arco inteiro desobstruído e plataforma alcançável; nunca saltar por cima de um título contando apenas com o pouso livre. Caminhada e corrida têm ciclos e velocidades próprias.

Margens declaradas de 18, 28, 32 ou 40 unidades no XAML não provam que cabe um corpo de 64 unidades. Corredores, laterais e topos só se tornam elegíveis depois de medição com layout, DPI, recortes e estados reais. Não reservar automaticamente todos os botões. Cada componente permitido deverá declarar lados, movimentos e dimensões úteis; exclusão, restauração, confirmação e ações destrutivas ficam fora das brincadeiras.

Em apoio superior, somente a âncora de contato fica no contorno; corpo, pernas e efeitos permanecem inteiramente na região livre acima. Sentar não permite deixar pernas sobre rótulos. Toque lateral termina no contorno exterior. Nenhuma cena chama comandos, simula teclas, move ponteiro, altera foco, muda tamanho/posição do controle ou muda seu estado de hover, seleção ou ativação.

A camada decorativa futura será passiva a mouse e teclado. Isso deve ser comprovado junto com leitura visual, pois permitir clique através do desenho não resolve texto encoberto. Não instalar botões transparentes sobre o mascote enquanto ele ocupa um apoio.

### Interrupção, saída e continuidade

- **I0, interrupção comum:** navegação; scroll; resize/DPI; atualização ou reordenação de coleção; apoio oculto/recortado; transição de layout; hover ou aproximação do ponteiro; foco/seleção/edição; abertura de tooltip, menu, calendário ou formulário; janela desativada, minimizada ou oculta; preferência de pausa, ocultação ou movimento reduzido.
- Antecipar recuo quando houver aproximação. Se o usuário já alcançou o controle, liberar sua leitura imediatamente. Não retardar input, modal ou navegação para terminar uma pose. Se não houver trajeto seguro ou pose de saída compatível, ocultar de modo breve/imediato e descartar a cena.
- **S0, saída comum:** terminar numa pose de ligação se houver tempo, recolher objeto/efeito, voltar por caminho ainda válido e cruzar totalmente uma borda interna livre. Se não couber, ocultar. Depois suspender a instância visual e iniciar cooldown; nunca deixar partículas ou objetos soltos.
- Quadro seguro não significa congelar um salto sobre texto. Uma retirada urgente pode ocultar o conjunto de uma vez. Fora dessas contingências, desenhar preparar → executar → acomodar → pose de ligação.
- Apenas uma instância visível. As janelas flutuantes, buscador, popup de macro e bandeja não recebem cópias do personagem. Não atravessar entre janelas. Ao voltar à principal, revalidar contexto e sortear apenas cenas atuais.
- A saudação de sessão de C02, C09 e ME-GREET/C59 compartilha um único marcador: executar apenas uma. C01 pode usar o aceno como saudação se o marcador ainda estiver livre; nas visitas seguintes usa um breve aceno de despedida antes de andar, sem reiniciar boas-vindas. Acenos contextuais de leitura/reação são mais breves e não reiniciam a saudação.
- Durações abaixo são faixas propostas para cena inteira, com entrada e saída; velocidade e cadência finais dependem dos sprites. Reações breves não devem prolongar operações rápidas artificialmente.

## 2. Fontes de elementos e estados

| Arquivo inspecionado | Elementos utilizados no planejamento |
| --- | --- |
| [MainWindow](../MacroHelper.UI/Views/MainWindow.xaml) e [MainViewModel](../MacroHelper.UI/ViewModels/MainViewModel.cs) | Casca, barra lateral, submenu de Macros, menu do avatar, CurrentView e mudança de página. |
| [DashboardView](../MacroHelper.UI/Views/DashboardView.xaml) e [DashboardViewModel](../MacroHelper.UI/ViewModels/DashboardViewModel.cs) | Saudação, Nova macro, três métricas, Mais usadas, tarefas do dia/atrasadas e compromissos de hoje. |
| [MacrosView](../MacroHelper.UI/Views/MacrosView.xaml) e [MacrosViewModel](../MacroHelper.UI/ViewModels/MacrosViewModel.cs) | Busca, favoritos, arquivadas, categoria, lista, gaveta, histórico, importação/exportação e cópia. |
| [NotasView](../MacroHelper.UI/Views/NotasView.xaml) e [NotasViewModel](../MacroHelper.UI/ViewModels/NotasViewModel.cs) | Busca, cartões, fixar/copiar, editor de tela, leitura e salvamento. |
| [TarefasView](../MacroHelper.UI/Views/TarefasView.xaml) e [TarefasViewModel](../MacroHelper.UI/ViewModels/TarefasViewModel.cs) | Filtros Abertas/Hoje/Atrasadas/Concluídas, recorrência, prazo, lembrete e formulário. |
| [CompromissosView](../MacroHelper.UI/Views/CompromissosView.xaml), [AgendaCalendarioView](../MacroHelper.UI/Views/AgendaCalendarioView.xaml) e [CompromissosViewModel](../MacroHelper.UI/ViewModels/CompromissosViewModel.cs) | Lembretes em Lista/Semana/Mês, Hoje, períodos, conflitos e faixa sem data. |
| [CategoriasView](../MacroHelper.UI/Views/CategoriasView.xaml) e [VariaveisGlobaisView](../MacroHelper.UI/Views/VariaveisGlobaisView.xaml) | Categoria pai/filha, cartões, criação, edição, avisos de uso e confirmação de exclusão. |
| [ConfiguracoesView](../MacroHelper.UI/Views/ConfiguracoesView.xaml) | Aparência, Personalização, Comportamento, Gatilho, atalhos e Saúde do App. |
| [AjudaView](../MacroHelper.UI/Views/AjudaView.xaml) | FAQ expansível e Atalhos úteis. Buscar na ajuda é apresentação estática, não busca funcional. |
| [ManualSqlView](../MacroHelper.UI/Views/ManualSqlView.xaml) e [code-behind](../MacroHelper.UI/Views/ManualSqlView.xaml.cs) | WebView2 e aviso de recurso ausente; área web excluída de percursos nesta proposta. |

## 3. Catálogo de sprites necessários

Todos os IDs abaixo são nomes propostos para produção. Não indicam arquivos existentes. Cada ação terá entrada, gesto/loop e retorno próprios, com âncora, duração, envelope por quadro, pontos de cancelamento e escala no manifesto futuro. A tabela não estima quantidade de desenhos antes de validar a arte.

| ID ou conjunto | Família | Conteúdo necessário |
| --- | --- | --- |
| CI-BASE | Corpo inteiro | Conjunto obrigatório: CI-PARADO, CI-VIRAR, CI-ANDAR-E, CI-ANDAR-D, CI-PARAR, CI-RECUAR e CI-SAIR. Fichas com CI-BASE exigem todos esses sprites. |
| CI-PARADO / CI-VIRAR / CI-PARAR | Corpo inteiro | Respiração mínima; transição cabeça/ombros/pés; acomodação de peso. |
| CI-ANDAR-E / CI-ANDAR-D | Corpo inteiro | Ciclos próprios para esquerda e direita, com contato e transferência de peso. |
| CI-CORRER-E / CI-CORRER-D / CI-ARRANCAR | Corpo inteiro | Corrida desenhada, suspensão, capa e aceleração; não acelerar a caminhada. |
| CI-RECUAR / CI-SAIR | Corpo inteiro | Retirada curta de um apoio e saída completa pela borda; versões compatíveis com poses finais. |
| CI-ACENAR / CI-DESPEDIR | Corpo inteiro | Braço legível e retorno a CI-PARADO; saudação e despedida distintas. |
| CI-LER-ABRIR / CI-LER-LOOP / CI-LER-GUARDAR | Corpo inteiro | Retirar papel do estojo, abrir, ler, virar/examinar o papel como variante desenhada e guardar antes de mover. |
| CI-PENSAR / CI-OLHAR / CI-ASSENTIR | Corpo inteiro | Queixo, observação de destinos e aprovação contida. |
| CI-TOCAR-E / CI-TOCAR-D | Corpo inteiro | Preparação, toque lateral no contorno e recolhimento, preservando mão dominante. |
| CI-SALTAR / CI-POUSAR / CI-DESCER | Corpo inteiro | Flexão, impulso, arco, absorção e descida de alturas permitidas. |
| CI-SENTAR / CI-SENTADO / CI-LEVANTAR-SENTADO | Corpo inteiro | Apoio das mãos, distribuição de peso e retirada; pés não cobrem controle. |
| CI-DEITAR / CI-DEITADO / CI-LEVANTAR-DEITADO | Corpo inteiro | Passagens por agachar/sentar e levantar com mãos/pés; não girar uma imagem rígida. |
| CI-BOCEJAR / CI-ESPREGUICAR / CI-CANECA | Corpo inteiro | Ociosidade com objeto que entra e sai do estojo; compatibilidade com apoio. |
| CI-MAGIA-ENTRAR / CI-MAGIA / CI-MAGIA-SAIR | Corpo inteiro | Preparação da pena, símbolos contidos e recolhimento; magia decorativa diferente do processamento. |
| CI-SURPRESA / CI-PEGAR-RUNA / CI-RUNAS | Corpo inteiro | Recuo curioso, tentativa de pegar runa e ordenação na própria mão; sem deixar objetos na interface. |
| CI-SUCESSO / CI-ERRO / CI-ATENCAO | Corpo inteiro | Reações contidas com recomposição: sucesso pode erguer a pena; erro olha a mão e baixa a pena; atenção leva a mão ao ouvido. Uso vinculado a resultado real. |
| FX-TOQUE / FX-RUNA / FX-MAGIA | Efeito do corpo | Envelope pequeno, transparência e ciclo de limpeza; sem badge funcional, partículas sobre texto ou mudança do controle. |
| ME-B0 / ME-BN / ME-ENTRAR / ME-SAIR | Mesa | Bases pronto para escrever e pergaminho de Notas; entrada/saída contextual da vinheta. |
| ME-WORK / ME-GREET / ME-THINK / ME-COFFEE | Mesa | work_ambient, greet, think e coffee do briefing, com ligações à B0. |
| ME-NOTAS-ENTRAR / ME-NOTAS-IDLE / ME-NOTAS-SAIR | Mesa | empty_notes_enter/idle/exit, transitando B0 → BN → B0. |
| ME-FIRST-NOTE / ME-SUCCESS / ME-ERROR | Mesa | first_note, success e error; condições funcionais explícitas. |
| RE-NEUTRO / RE-LENDO / RE-ATENTO | Retrato | Adaptações estáticas próprias para pequeno tamanho; sem animação de rosto reduzida automaticamente de outra família. |

Orientações laterais não devem trocar cicatriz, espada, ombreira ou mão dominante sem revisão de arte. Não presumir espelhamento automático. CI não leva mesa; ME não anda. Retratos nunca substituem ícones funcionais existentes. BO/offline e sequências de conexão do briefing não são necessárias ao núcleo local offline; permanecem condicionais a uma funcionalidade futura realmente dependente de conexão.

## 4. Fichas das cenas

**Leitura das fichas:** G, D, R, I, P, I0 e S0 têm o significado obrigatório da seção 1. Todas as fichas estão em estado **Proposta / execução não verificada**. As variações compartilham a mesma coreografia; não são ações aleatórias em qualquer quadro.

### C01 — Passeio completo do escriba (item 31 do briefing)

- **Local e elemento real:** Início: lateral e, se aprovado, topo do botão Nova macro; alternativas reais: Nova Nota, Nova Tarefa e Nova Categoria nas respectivas listas.
- **Gatilho:** D + G; longa ociosidade; dois pontos de leitura/decisão, apoio de deitar e percurso completo previamente válidos.
- **Percurso:** Borda interna → corredor A → pausa de leitura → corredor B → lateral do botão → arco ao topo → descida → corredor de corrida → borda.
- **Ações:** Entrar, acenar, andar, abrir/ler/guardar, andar, pensar, tocar borda, pensar, saltar/pousar, deitar, levantar, conjurar, descer, arrancar, correr e escapar.
- **Variações:** Alternar tela/lado; 1–2 toques; leitura e descanso curtos/longos. Se o topo não couber, a cena completa fica inelegível; usar C03 ou C04.
- **Duração:** 35–65 s; leitura 3–6 s e deitado 4–8 s; no máximo uma vez por sessão elegível como proposta inicial.
- **Interrupções:** I0; cancelamentos após aceno, papel guardado, toque recolhido e antes do salto. Sobre apoio, descer só com arco livre; input urgente força ocultação.
- **Saída:** S0 após corrida; remover toda magia e manter uma única instância.
- **Sprites necessários:** CI-BASE; CI-ACENAR; CI-LER-ABRIR/LOOP/GUARDAR; CI-PENSAR; CI-TOCAR-E/D; CI-SALTAR; CI-POUSAR; CI-DEITAR/DEITADO/LEVANTAR-DEITADO; CI-MAGIA-ENTRAR/MAGIA/SAIR; CI-DESCER; CI-ARRANCAR; CI-CORRER-E/D; FX-TOQUE; FX-MAGIA; CI-DESPEDIR.

### C02 — Chegada discreta pela margem

- **Local e elemento real:** MainWindow, corredor livre do painel CurrentView na tela carregada; fora de cabeçalho, navegação e rodapé de estado.
- **Gatilho:** D + G; primeira janela principal ativa da sessão, depois do carregamento real.
- **Percurso:** Borda interna lateral → primeiro ponto livre → mesma borda.
- **Ações:** Surgir progressivamente, acomodar pés, olhar o ambiente, acenar uma vez e sair andando.
- **Variações:** Entrada esquerda/direita validada; aceno curto ou saudação de cabeça; sem repetir a cada navegação.
- **Duração:** 4–7 s; uma chegada por sessão proposta.
- **Interrupções:** I0; menu do avatar e submenu da barra lateral são obstáculos, não lugares para aparecer.
- **Saída:** S0 sem permanecer na barra lateral.
- **Sprites necessários:** CI-BASE; CI-ACENAR; CI-OLHAR; CI-DESPEDIR; CI-ASSENTIR.

### C03 — Leitura durante uma caminhada

- **Local e elemento real:** Margem livre das listas de Macros, Notas, Categorias ou Variáveis Globais.
- **Gatilho:** D + G; sem editor e com ponto de repouso lateral fora de pesquisa e cartões.
- **Percurso:** Borda → corredor externo → bolsão de leitura → retorno.
- **Ações:** Parar, retirar pergaminho, ler duas linhas fictícias, olhar para o usuário, guardar e continuar.
- **Variações:** Lado de entrada; duas paradas possíveis previamente aprovadas; uma ou duas pausas de leitura.
- **Duração:** 10–17 s.
- **Interrupções:** I0; guardar papel antes da locomoção normal; ocultar conjunto se texto/controle surgir na região.
- **Saída:** S0 por caminhada após papel recolhido.
- **Sprites necessários:** CI-BASE; CI-LER-ABRIR/LOOP/GUARDAR; CI-OLHAR.

### C04 — Curiosidade lateral com um botão

- **Local e elemento real:** Lateral de Nova macro, Nova Nota, Nova Tarefa, Novo lembrete, Nova Categoria ou Nova Variável; componente explicitamente permitido.
- **Gatilho:** D + G; lateral livre, botão fora de foco/hover e nenhum popup associado aberto.
- **Percurso:** Borda → corredor → distância de parada exterior ao botão → recuo.
- **Ações:** Observar contorno, levar mão ao queixo, encostar uma vez na borda, olhar a própria mão e se afastar.
- **Variações:** Um ou dois toques; lado permitido; desistir após pensar. Não exige topo disponível.
- **Duração:** 7–12 s.
- **Interrupções:** I0; proximidade do ponteiro e foco do botão encerram imediatamente a brincadeira.
- **Saída:** S0 pelo lado da aproximação; FX-TOQUE recolhido sem modificar o botão.
- **Sprites necessários:** CI-BASE; CI-PENSAR; CI-TOCAR-E/D; CI-OLHAR; FX-TOQUE.

### C05 — Runa fujona no corredor

- **Local e elemento real:** MainWindow / margem externa livre da tela atual; jamais sobre a barra lateral ou lista densa.
- **Gatilho:** D + G; corredor suficientemente largo e saída livre para corrida.
- **Percurso:** Borda → ponto aberto de magia → segmento livre → borda de saída.
- **Ações:** Conjurar uma runa, tentar pegá-la, reagir com surpresa, arrancar e correr atrás até sair.
- **Variações:** Esquerda/direita; uma pausa antes de correr; salto opcional apenas em segmento cujo arco esteja aprovado.
- **Duração:** 8–14 s.
- **Interrupções:** I0; se o corredor fechar, recolher runa e recuar ou ocultar, sem cruzar dados.
- **Saída:** S0 com runa desaparecendo junto do personagem.
- **Sprites necessários:** CI-BASE; CI-MAGIA-ENTRAR/MAGIA/SAIR; CI-PEGAR-RUNA; CI-SURPRESA; CI-ARRANCAR; CI-CORRER-E/D; CI-SALTAR; CI-POUSAR; FX-RUNA.

### C06 — Pequeno salto em espaço aberto

- **Local e elemento real:** Bolsão livre adjacente a uma lista, fora da área clicável de linhas, campos e cartões.
- **Gatilho:** D + G; mesmo chão seguro para impulso e pouso, arco desobstruído.
- **Percurso:** Borda → aproximação → arco curto entre dois pontos do mesmo corredor → borda.
- **Ações:** Olhar um ponto, flexionar, saltar curto, pousar, acomodar a postura e seguir.
- **Variações:** Salto para esquerda/direita; pausa de observação antes ou depois, nunca dois saltos sobre texto.
- **Duração:** 6–10 s.
- **Interrupções:** I0; conferir arco novamente antes do impulso; interrupção urgente durante voo oculta o conjunto.
- **Saída:** S0 sem usar células de agenda como degraus.
- **Sprites necessários:** CI-BASE; CI-OLHAR; CI-SALTAR; CI-POUSAR.

### C07 — Ceder passagem ao usuário

- **Local e elemento real:** Qualquer apoio já elegível ocupado por SKald na janela principal.
- **Gatilho:** I; aproximação do ponteiro, foco de teclado ou abertura iminente de um controle.
- **Percurso:** Apoio → recuo curto para bolsão livre; se inexistente, sem percurso visível.
- **Ações:** Recolher mão/objeto, descer do apoio se seguro, dar passo atrás; ocultar imediatamente quando necessário.
- **Variações:** Recuo lateral, descida curta ou ocultação; não exigir corrida completa.
- **Duração:** 0–1 s para liberar a região; não atrasar nenhum input.
- **Interrupções:** Uso efetivo do controle prevalece sobre a própria retirada; hover não pode virar perseguição repetitiva.
- **Saída:** Ocultar e suspender até novo G e cooldown; não reaparecer no mesmo apoio enquanto em uso.
- **Sprites necessários:** CI-BASE; CI-LER-GUARDAR; CI-DESCER; CI-LEVANTAR-SENTADO; CI-LEVANTAR-DEITADO.

### C08 — Despedida antes da troca de contexto

- **Local e elemento real:** MainWindow / transição de CurrentView, menu do avatar, minimização ou abertura de janela auxiliar.
- **Gatilho:** I; navegação ou perda de visibilidade/ativação.
- **Percurso:** Se houver tempo, ponto atual → borda próxima; na troca imediata, nenhuma travessia.
- **Ações:** Baixar gesto, recolher efeito, despedir-se somente se já em pé e com espaço; retirar a instância.
- **Variações:** Aceno de despedida sem percurso ou ocultação direta; nunca animar por cima da nova tela.
- **Duração:** 0–1 s; não bloquear navegação ou fechamento.
- **Interrupções:** Troca imediata, formulário ou minimização cancela a despedida visual e oculta.
- **Saída:** Sem fila pendente; revalidar ao retornar e não continuar trajeto antigo.
- **Sprites necessários:** CI-BASE; CI-DESPEDIR; CI-LER-GUARDAR; CI-MAGIA-SAIR.

### C09 — Saudação ao Início

- **Local e elemento real:** DashboardView / exterior livre do cartão de saudação Olá e botão Nova macro.
- **Gatilho:** D + G; primeira visita ociosa ao Início na sessão, após dados e transição.
- **Percurso:** Borda do painel → lateral do cartão de saudação → saída.
- **Ações:** Olhar a data de longe, acenar ao usuário, baixar o braço e sair.
- **Variações:** Entrada de dois lados permitidos; aceno ou assentimento; sem ler ou registrar nome do usuário.
- **Duração:** 5–8 s.
- **Interrupções:** I0; clique em Nova macro ou qualquer navegação tem prioridade.
- **Saída:** S0 sem pisar nos chips /atalho ou Ctrl+Espaço.
- **Sprites necessários:** CI-BASE; CI-ACENAR; CI-ASSENTIR.

### C10 — Conferência das três métricas

- **Local e elemento real:** DashboardView / margens externas dos cartões macros cadastradas, usos hoje e economizados este mês.
- **Gatilho:** D + G; métricas carregadas; caminho só entre regiões externas comprovadamente largas.
- **Percurso:** Borda → ponto de observação diante do conjunto → segundo ponto externo opcional → borda.
- **Ações:** Consultar pergaminho fictício, olhar os cartões, pensar e guardar o papel.
- **Variações:** Observar um ou dois cartões; não usar valor da métrica para inventar prêmio ou marco.
- **Duração:** 9–15 s.
- **Interrupções:** I0; mudança dos números ou do layout interrompe e remapeia.
- **Saída:** S0 sem mover ou ressaltar números.
- **Sprites necessários:** CI-BASE; CI-LER-ABRIR/LOOP/GUARDAR; CI-PENSAR; CI-OLHAR.

### C11 — Descanso na borda de Mais usadas

- **Local e elemento real:** DashboardView / topo exterior do cartão Macros mais usadas, distante do título e rótulo ESTE MÊS.
- **Gatilho:** D + G; topo com largura e altura para sentar; ranking carregado e estável.
- **Percurso:** Borda → lateral do cartão → arco curto ao apoio superior → descida.
- **Ações:** Pensar, subir, sentar acima do cartão, observar o ambiente, levantar e descer.
- **Variações:** Sentado de frente ou em três quartos; um bocejo em ociosidade longa; se pernas invadirem texto, inelegível.
- **Duração:** 12–20 s.
- **Interrupções:** I0; scroll, atualização do ranking ou apontamento para cartão abortam o repouso.
- **Saída:** S0 depois de descida validada.
- **Sprites necessários:** CI-BASE; CI-PENSAR; CI-SALTAR; CI-POUSAR; CI-SENTAR/SENTADO/LEVANTAR-SENTADO; CI-BOCEJAR; CI-DESCER; CI-OLHAR.

### C12 — Pergaminho de um mês ainda sem usos

- **Local e elemento real:** DashboardView / espaço exterior ao estado Nenhuma macro usada este mês.
- **Gatilho:** D + G; consulta do ranking concluída com sucesso e sem itens; não significa ausência de macros cadastradas.
- **Percurso:** Borda → pausa ao lado do bloco vazio → borda.
- **Ações:** Abrir papel com poucas runas, esperar, fechar e dar pequeno aceno.
- **Variações:** Ler em pé ou apenas consultar a capa do papel; nenhuma celebração de produtividade.
- **Duração:** 8–12 s.
- **Interrupções:** I0; primeiro resultado de uso invalida o estado; clique em Nova macro encerra.
- **Saída:** S0 com papel guardado.
- **Sprites necessários:** CI-BASE; CI-LER-ABRIR/LOOP/GUARDAR; CI-ACENAR.

### C13 — Caneca num dia sem tarefas vencendo

- **Local e elemento real:** DashboardView / exterior do bloco Nada vencendo hoje.
- **Gatilho:** D + G; lista de hoje/atrasadas carregada vazia; outros prazos e tarefas podem existir.
- **Percurso:** Borda → apoio lateral ou superior com volume completo → borda.
- **Ações:** Parar, sentar se houver apoio, tomar pequeno gole, guardar caneca e levantar.
- **Variações:** Em pé se faltar plataforma; repouso curto ou bocejo; sem afirmar que todo trabalho acabou.
- **Duração:** 10–17 s.
- **Interrupções:** I0; nova tarefa na lista ou novo compromisso exige remapeamento.
- **Saída:** S0 com caneca guardada; levantar e descer por arco validado se tiver usado topo.
- **Sprites necessários:** CI-BASE; CI-CANECA; CI-SENTAR/SENTADO/LEVANTAR-SENTADO; CI-BOCEJAR; CI-SALTAR; CI-POUSAR; CI-DESCER.

### C14 — Olhar atento aos compromissos do dia

- **Local e elemento real:** DashboardView / exterior do cartão Com hora marcada hoje e botão Ver todos.
- **Gatilho:** D + G; cartão realmente visível e com compromissos; sem notificação ativa.
- **Percurso:** Borda → lateral externa do cartão → saída pelo mesmo corredor.
- **Ações:** Consultar pergaminho, olhar para o cabeçalho e guardar o papel, sem apontar um compromisso particular.
- **Variações:** Curta inclinação de cabeça; leitura mais breve quando a lista ocupa espaço maior.
- **Duração:** 7–12 s.
- **Interrupções:** I0; cartão desaparece quando vazio; notificação real passa à prioridade funcional, não à cena decorativa.
- **Saída:** S0 sem atravessar horários, locais ou o botão Ver todos.
- **Sprites necessários:** CI-BASE; CI-LER-ABRIR/LOOP/GUARDAR; CI-OLHAR.

### C15 — Aceno depois de concluir tarefa no Início

- **Local e elemento real:** DashboardView / corredor exterior ao cartão Hoje e atrasadas.
- **Gatilho:** R + G; conclusão confirmada pelo serviço e recarga da lista correspondente concluída. O comando atual não usa o retorno para produzir evento tipado; integração pendente.
- **Percurso:** Aparecer num ponto livre próximo, sem perseguir a linha removida → mesma borda.
- **Ações:** Assentir uma vez, guardar uma pequena runa na mão e sair.
- **Variações:** Meio sorriso ou assentimento; não celebrar apenas porque a linha sumiu por filtro ou atualização.
- **Duração:** 2–4 s.
- **Interrupções:** I0; falha, reversão, navegação ou operação seguinte descarta a reação.
- **Saída:** S0; não acumular uma reação para cada item concluído em série.
- **Sprites necessários:** CI-BASE; CI-SUCESSO; CI-ASSENTIR; CI-RUNAS.

### C16 — Patrulha da lista de macros

- **Local e elemento real:** MacrosView / corredor externo à lista, abaixo do cabeçalho e fora da barra de pesquisa/filtros.
- **Gatilho:** D + G; lista carregada, sem gaveta nem menu de linha.
- **Percurso:** Borda → dois pontos externos ao painel da lista → saída oposta se ligada.
- **Ações:** Caminhar, parar, observar os cartões, ajustar postura e continuar.
- **Variações:** Percurso curto ou médio; direção alternada; corrida apenas em segmento aprovado, sem varrer linha a linha.
- **Duração:** 9–16 s.
- **Interrupções:** I0; busca, categoria, favoritos, arquivadas ou reordenação invalidam o percurso.
- **Saída:** S0 sem atravessar conteúdo ou botões de linha.
- **Sprites necessários:** CI-BASE; CI-OLHAR; CI-PENSAR; CI-ARRANCAR; CI-CORRER-E/D.

### C17 — Curiosidade com Favoritos

- **Local e elemento real:** MacrosView / lado exterior livre do botão de filtro SomenteFavoritos.
- **Gatilho:** D + G; filtro estável e botão explicitamente elegível só para observação lateral.
- **Percurso:** Borda → ponto de observação externo ao grupo de filtros → borda.
- **Ações:** Olhar para o símbolo de favorito, retirar pequena runa do estojo para a própria mão, compará-la visualmente e recolher.
- **Variações:** Runa simples ou olhar pensativo; não tocar se o grupo apertado não reservar lateral suficiente.
- **Duração:** 6–10 s.
- **Interrupções:** I0; hover/foco e mudança de filtro encerram; favorito de cada macro continua intocado.
- **Saída:** S0 sem acender/desligar estrela da interface.
- **Sprites necessários:** CI-BASE; CI-OLHAR; CI-RUNAS; FX-RUNA.

### C18 — Consulta tranquila às arquivadas

- **Local e elemento real:** MacrosView / margem lateral da lista com MostrarArquivadas ativo.
- **Gatilho:** D + G; filtro de arquivadas concluído e estável.
- **Percurso:** Borda → bolsão externo da lista → borda.
- **Ações:** Abrir pergaminho antigo, ler, pensar e enrolá-lo; tom discreto.
- **Variações:** Pausa mais longa ou duas olhadas; não representar reativação, exclusão ou restauração.
- **Duração:** 10–16 s.
- **Interrupções:** I0; desarquivar, mudar filtro ou abrir formulário interrompe.
- **Saída:** S0 com papel recolhido; nenhum efeito sobre status Ativo.
- **Sprites necessários:** CI-BASE; CI-LER-ABRIR/LOOP/GUARDAR; CI-PENSAR.

### C19 — Busca de macro sem correspondência

- **Local e elemento real:** MacrosView / exterior do estado vazio após TermoBusca ou filtros.
- **Gatilho:** D contextual + G; pesquisa atual concluída sem erro, nenhum resultado, digitação encerrada e campo sem foco. Não inferir banco vazio.
- **Percurso:** Borda → ponto de pausa fora da caixa de busca e mensagem vazia → retorno.
- **Ações:** Consultar pergaminho, virar o papel, pensar brevemente e guardar; não sinalizar falha.
- **Variações:** Um olhar ou pequeno encolher de ombros; sem indicar que a macro não existe globalmente.
- **Duração:** 6–10 s.
- **Interrupções:** I0; qualquer tecla, filtro, novo resultado ou consulta em andamento elimina a cena.
- **Saída:** S0 sem limpar a busca.
- **Sprites necessários:** CI-BASE; CI-LER-ABRIR/LOOP/GUARDAR; CI-PENSAR; CI-OLHAR.

### C20 — Registro de macro salvo

- **Local e elemento real:** MacrosView / margem externa após a gaveta PainelFormulario fechar e a lista recarregar.
- **Gatilho:** R + G; evento Salvo de MacroFormViewModel correspondente à operação concluída, com adaptador futuro correlacionado.
- **Percurso:** Entrada curta num bolsão fora do toast → mesma borda.
- **Ações:** Erguer pena, confirmar com cabeça e recolher uma runa discreta.
- **Variações:** Criação ou edição distinguida apenas pelo gesto; mesmo sucesso sem prêmio inventado.
- **Duração:** 2–4 s.
- **Interrupções:** I0; formulário reaberto, novo erro ou navegação descarta; nunca animar sobre Salvar ou ErroMensagem.
- **Saída:** S0; não atrasar fechamento do formulário.
- **Sprites necessários:** CI-BASE; CI-SUCESSO; CI-ASSENTIR; FX-RUNA.

### C21 — Cópia de macro confirmada

- **Local e elemento real:** MacrosView / bolsão exterior à lista e ao toast Mensagem.
- **Gatilho:** R + G; CopiarConteudo concluiu escrita na área de transferência sem exceção; integração futura observa resultado, não o texto do toast.
- **Percurso:** Entrada curta na margem → parada → borda.
- **Ações:** Retirar o próprio pergaminho, desenrolar só a ponta, enrolar, assentir e guardá-lo; sem transportar o conteúdo copiado.
- **Variações:** Papel fechado em uma ou duas mãos; gesto único para várias cópias rápidas.
- **Duração:** 2–3 s.
- **Interrupções:** I0; falha de clipboard, navegação ou usuário indo copiar novamente elimina a reação.
- **Saída:** S0 sem alterar a área de transferência.
- **Sprites necessários:** CI-BASE; CI-LER-GUARDAR; CI-ASSENTIR; CI-LER-ABRIR.

### C22 — Importação concluída

- **Local e elemento real:** MacrosView / margem exterior da lista após fechar o seletor de arquivo e terminar ImportarJson ou ImportarCsv.
- **Gatilho:** R + G; resultado da importação confirmado e recarga atual concluída; não basta escolher arquivo.
- **Percurso:** Borda → ponto externo ao conjunto atualizado → borda.
- **Ações:** Trazer duas runas fictícias na mão, alinhá-las, guardar e assentir.
- **Variações:** JSON/CSV compartilham coreografia; quantidade de runas decorativa, sem equivaler a registros importados.
- **Duração:** 3–5 s.
- **Interrupções:** I0; cancelamento do seletor não reage; importação parcial/aviso exige preservar mensagem e não celebrar como sucesso integral.
- **Saída:** S0 sem visitar todos os registros importados.
- **Sprites necessários:** CI-BASE; CI-RUNAS; CI-SUCESSO; CI-ASSENTIR.

### C23 — Exportação registrada

- **Local e elemento real:** MacrosView / corredor livre após SaveFileDialog e ExportarJson retornarem com gravação concluída.
- **Gatilho:** R + G; resultado de gravação confirmado; extensão ou seleção do destino não comprovam sucesso.
- **Percurso:** Entrada → pequena pausa fora da mensagem → saída.
- **Ações:** Retirar e abrir parcialmente o próprio pergaminho, fechá-lo, prender no estojo e fazer gesto de tarefa concluída.
- **Variações:** Assentimento ou saudação mínima; sem representar backup integral do banco.
- **Duração:** 2–4 s.
- **Interrupções:** I0; cancelamento ou falha de escrita elimina sucesso; não reaparecer se usuário já saiu da tela.
- **Saída:** S0 sem abrir o arquivo exportado.
- **Sprites necessários:** CI-BASE; CI-LER-GUARDAR; CI-SUCESSO; CI-LER-ABRIR.

### C24 — Falha operacional com recomposição

- **Local e elemento real:** MacrosView ou NotasView / bolsão exterior à mensagem de falha de copiar/importar/exportar, fora de formulários.
- **Gatilho:** R + G; falha real correlacionada da operação atual; nunca usar para cancelamento, campo vazio ou busca sem resultados.
- **Percurso:** Ponto seguro já disponível → recuo mínimo → borda.
- **Ações:** Olhar a própria mão, baixar a pena, recompor postura e retirar-se; mensagem do aplicativo continua sendo a explicação.
- **Variações:** Suspiro ou sobrancelha levantada; nenhuma magia de correção ou confirmação de recuperação.
- **Duração:** 2–3 s.
- **Interrupções:** I0; se houver editor, diálogo, texto extenso de erro ou foco em recuperação, permanecer oculto.
- **Saída:** S0 sem sugerir tentativa automática; nova tentativa pertence ao usuário.
- **Sprites necessários:** CI-BASE; CI-ERRO; CI-OLHAR.

### C25 — Mesa e pergaminho de Notas realmente vazias

- **Local e elemento real:** NotasView / futura área reservada junto ao estado Nenhuma nota por aqui, sem ocupar texto ou Nova Nota.
- **Gatilho:** P + G; consulta concluída com sucesso, busca vazia e ausência global de notas confirmada; coleção vazia durante loading ou filtragem não basta.
- **Percurso:** Sem circulação; vinheta surge apenas no slot futuro validado.
- **Ações:** ME-B0 → abrir pergaminho → ME-BN; movimento mínimo de leitura, pausa e fechamento.
- **Variações:** Pausa de 6–10 s entre pequenos movimentos; retrato RE-LENDO se só houver slot compacto aprovado; sem slot, oculto.
- **Duração:** Entrada 2 s; loop de 1 s com pausa de 6–10 s, máximo 20 s nesta visita; saída 2 s.
- **Interrupções:** I0; qualquer resultado, nova consulta, editor ou digitação encerra; não manter mesa atrás de formulário.
- **Saída:** ME-NOTAS-SAIR → ME-B0 → ME-SAIR; em urgência ocultar vinheta inteira.
- **Sprites necessários:** ME-B0; ME-BN; ME-ENTRAR/SAIR; ME-NOTAS-ENTRAR/IDLE/SAIR; RE-LENDO.

### C26 — Pergaminho que não encontra a busca

- **Local e elemento real:** NotasView / margem externa do estado vazio, com TermoBusca preenchido.
- **Gatilho:** D contextual + G; última consulta correlacionada terminou sem erro, zero resultados e campo sem foco; não confundir com C25.
- **Percurso:** Borda → pausa lateral longe do placeholder Buscar nas notas → borda.
- **Ações:** Abrir pergaminho, procurar um símbolo fictício, pensar, fechar e sair calmamente.
- **Variações:** Uma ou duas consultas ao papel; sem selo de erro e sem convite obrigatório para criar nota.
- **Duração:** 7–11 s.
- **Interrupções:** I0; debounce/consulta nova, digitação, limpar busca ou chegada de resultados cancela.
- **Saída:** S0 sem mudar TermoBusca.
- **Sprites necessários:** CI-BASE; CI-LER-ABRIR/LOOP/GUARDAR; CI-PENSAR.

### C27 — Leitor ao lado dos cartões de Notas

- **Local e elemento real:** NotasView / bolsão externo à grade de cartões, fora de Titulo, Resumo e DataAtualizacao.
- **Gatilho:** D + G; cartões carregados, nenhum editor nem confirmação aberta.
- **Percurso:** Borda → ponto externo ao primeiro cartão visível elegível → pequena volta → borda.
- **Ações:** Ler o próprio pergaminho, olhar para os cartões, guardar o papel e caminhar.
- **Variações:** Sentar em topo aprovado do painel, caso corpo e pernas permaneçam fora do conteúdo; caso contrário, ler em pé.
- **Duração:** 12–18 s.
- **Interrupções:** I0; abrir nota, scroll ou mudança da grade exige retirada, sem prender âncora ao índice do cartão.
- **Saída:** S0 com papel guardado e descida validada se sentado.
- **Sprites necessários:** CI-BASE; CI-LER-ABRIR/LOOP/GUARDAR; CI-OLHAR; CI-SENTAR/SENTADO/LEVANTAR-SENTADO; CI-DESCER; CI-SALTAR; CI-POUSAR.

### C28 — Observação de uma nota fixada

- **Local e elemento real:** NotasView / exterior de cartão com indicador Fixada, longe dos botões Fixar/Copiar/Editar/Excluir.
- **Gatilho:** D + G; pelo menos um cartão fixado visível e geometria estável após eventual ordenação.
- **Percurso:** Borda → observação lateral do cartão → borda.
- **Ações:** Olhar o indicador de alfinete, inclinar a cabeça, assentir e seguir.
- **Variações:** Olhar de longe ou pequena pausa; não tocar no alfinete nem reproduzir evento de fixação.
- **Duração:** 6–10 s.
- **Interrupções:** I0; FixarAsync ou reordenação cancela; alvo deve ser elemento visível atual, nunca posição salva da lista.
- **Saída:** S0 sem simular fixar/desafixar.
- **Sprites necessários:** CI-BASE; CI-OLHAR; CI-ASSENTIR.

### C29 — Primeira nota salva, quando comprovada

- **Local e elemento real:** NotasView / margem livre após salvar, fechar editor e concluir recarga; alternativa de mesa apenas em slot futuro.
- **Gatilho:** R + G; criação persistida com contagem global anterior igual a zero e posterior maior que zero, comprovadas. Esse marco não é exposto hoje e requer contrato futuro.
- **Percurso:** Entrada curta na margem ou vinheta parada no slot; nenhuma ida ao botão Salvar.
- **Ações:** Abrir pequeno pergaminho com primeira runa fictícia, sorrir, guardar e assentir.
- **Variações:** CI-SUCESSO em corpo inteiro; na mesa, ME-ENTRAR → ME-B0 → ME-NOTAS-ENTRAR → ME-BN → ME-FIRST-NOTE → ME-B0, seguindo ligações desenhadas. Não trocar famílias na mesma posição sem transição contextual.
- **Duração:** Corpo inteiro: 3–5 s. Variante de mesa: 7–10 s para incluir entrada, chegada a BN, first_note e saída; nunca atrasar o resultado funcional para exibi-la.
- **Interrupções:** I0; edição, filtro vazio, cópia, carga inicial ou tentativa falha não disparam; operação sem evidência é inelegível.
- **Saída:** S0 para corpo; ME-SAIR para mesa; reação expira ao trocar de contexto.
- **Sprites necessários:** CI-BASE; CI-LER-ABRIR/GUARDAR; CI-SUCESSO; ME-B0; ME-ENTRAR/SAIR; ME-FIRST-NOTE; ME-BN; ME-NOTAS-ENTRAR; CI-ASSENTIR.

### C30 — Conteúdo de nota copiado

- **Local e elemento real:** NotasView / margem externa após CopiarConteudo retornar sem exceção.
- **Gatilho:** R + G; resultado de clipboard confirmado para a nota atual; não interpretar apenas MensagemSucesso ou clique.
- **Percurso:** Borda → pausa segura exterior ao toast → retorno.
- **Ações:** Retirar e abrir parcialmente um pergaminho fictício, fechar, guardar no estojo, assentir de modo discreto e sair.
- **Variações:** Assentimento curto ou pequeno sorriso; não duplicar texto da nota em efeitos.
- **Duração:** 2–3 s.
- **Interrupções:** I0; falha de clipboard, abrir nota ou nova operação descarta; sem fila de várias cópias.
- **Saída:** S0 sem ler ou registrar conteúdo copiado.
- **Sprites necessários:** CI-BASE; CI-LER-GUARDAR; CI-ASSENTIR; CI-SUCESSO; CI-LER-ABRIR.

### C31 — Silêncio ao entrar no editor de Notas

- **Local e elemento real:** NotasView / MostrarEditor, CaixaDeConteudo, BarraFixa e barra contextual de seleção.
- **Gatilho:** I; abrir/criar nota, mudar para Escrever, selecionar texto ou iniciar composição.
- **Percurso:** Do ponto atual a saída curta se livre; nenhuma circulação dentro do editor ou modo Ver.
- **Ações:** Recolher pergaminho e ocultar antes de competir com texto, formatação, título, link, caracteres especiais ou Salvar.
- **Variações:** Saída curta se já fora do editor; ocultação direta quando a sobreposição ocupa o painel.
- **Duração:** 0–1 s; edição e seleção liberadas imediatamente.
- **Interrupções:** Editor ou popup já visível cancela a própria retirada animada.
- **Saída:** Permanecer oculto enquanto MostrarEditor; retomada decorativa só após fechamento, mapa novo e cooldown.
- **Sprites necessários:** CI-BASE; CI-LER-GUARDAR; CI-MAGIA-SAIR.

### C32 — Volta da biblioteca após fechar uma nota

- **Local e elemento real:** NotasView / margem externa da grade, depois de FecharEditor e estabilização do layout.
- **Gatilho:** D + G; editor fechado e intervalo decorativo atendido; fechamento não implica salvamento.
- **Percurso:** Borda diferente da usada antes da edição → pausa lateral → borda.
- **Ações:** Voltar andando com papel guardado, pensar diante da grade e escolher saída.
- **Variações:** Breve leitura do próprio papel ou simples observação; sem reação de sucesso por Cancelar/Fechar.
- **Duração:** 7–12 s.
- **Interrupções:** I0; reabrir editor, pesquisa ou confirmação de exclusão aborta.
- **Saída:** S0; não continuar a cena que existia antes do editor.
- **Sprites necessários:** CI-BASE; CI-PENSAR; CI-OLHAR; CI-LER-ABRIR/LOOP/GUARDAR.

### C33 — Ronda das tarefas abertas

- **Local e elemento real:** TarefasView / corredor externo à lista com filtro Abertas.
- **Gatilho:** D + G; lista filtrada carregada, sem formulário/confirmando exclusão.
- **Percurso:** Borda → ponto de observação externo à lista → segunda pausa livre → borda.
- **Ações:** Andar, consultar papel, guardar e continuar com passada concentrada.
- **Variações:** Percurso curto quando lista densa; duas direções; sem acompanhar individualmente checkboxes.
- **Duração:** 10–16 s.
- **Interrupções:** I0; conclusão, edição, filtro ou reordenação cancela a geometria atual.
- **Saída:** S0 sem marcar tarefa.
- **Sprites necessários:** CI-BASE; CI-LER-ABRIR/LOOP/GUARDAR; CI-OLHAR.

### C34 — Descanso quando o filtro Hoje está vazio

- **Local e elemento real:** TarefasView / exterior do bloco Nada por aqui com filtro Hoje.
- **Gatilho:** D + G; consulta e filtro atuais concluídos com sucesso; vazio se refere apenas a Hoje.
- **Percurso:** Borda → ponto de repouso lateral → borda.
- **Ações:** Sentar se houver apoio permitido, tomar gole breve, levantar e seguir; sem afirmar que não existem tarefas.
- **Variações:** Em pé com caneca se faltar topo; bocejo somente em ociosidade prolongada.
- **Duração:** 10–16 s.
- **Interrupções:** I0; nova tarefa, mudança do dia, filtro ou formulário interrompe.
- **Saída:** S0, recolhendo caneca e deixando mensagem do estado vazio livre.
- **Sprites necessários:** CI-BASE; CI-CANECA; CI-SENTAR/SENTADO/LEVANTAR-SENTADO; CI-BOCEJAR; CI-SALTAR; CI-POUSAR; CI-DESCER.

### C35 — Conferência sóbria das atrasadas

- **Local e elemento real:** TarefasView / margem externa da lista com filtro Atrasadas.
- **Gatilho:** D + G; filtro carregado e pelo menos um item; usuário sem manipular a lista.
- **Percurso:** Borda → ponto externo de leitura → retorno curto.
- **Ações:** Consultar papel, pensar e guardar, com gesto contido; sem cobrança, pânico ou comemoração.
- **Variações:** Uma pausa de observação ou mão no queixo; cenas longas/corrida têm peso reduzido nesse contexto.
- **Duração:** 6–10 s.
- **Interrupções:** I0; hover, foco, concluir ou editar encerra; notificação real não é simulada.
- **Saída:** S0 sem destacar tarefa específica nem reproduzir seus dados.
- **Sprites necessários:** CI-BASE; CI-LER-ABRIR/LOOP/GUARDAR; CI-PENSAR.

### C36 — Confirmação de tarefa concluída

- **Local e elemento real:** TarefasView / bolsão exterior à lista após ConcluirAsync.
- **Gatilho:** R + G; retorno ok da operação de conclusão, tarefa antes aberta, persistência e recarga correspondentes confirmadas.
- **Percurso:** Entrada curta → ponto livre próximo ao painel → borda.
- **Ações:** Assentir, baixar a pena e fazer sorriso mínimo.
- **Variações:** Conclusão simples; abrir/reabrir não usa a mesma celebração. Agrupar reações durante uso intenso.
- **Duração:** 2–4 s.
- **Interrupções:** I0; retorno de erro, nova ação, reabertura ou saída da tela descarta.
- **Saída:** S0 sem esperar uma animação para atualizar checkbox/lista.
- **Sprites necessários:** CI-BASE; CI-SUCESSO; CI-ASSENTIR.

### C37 — Próxima ocorrência de tarefa recorrente

- **Local e elemento real:** TarefasView / exterior da lista e indicador RecorrenciaTexto, após conclusão.
- **Gatilho:** R + G; conclusão persistida e criação da próxima ocorrência realmente confirmada; não basta existir texto de recorrência. Esta reação substitui C36 para a mesma operação; não empilhar duas comemorações.
- **Percurso:** Ponto livre da margem → passo curto → borda.
- **Ações:** Guardar uma runa e retirar outra ainda apagada, olhar para ela e recolher; gesto de continuidade da rotina.
- **Variações:** Uma volta curta do pulso ou troca entre mãos; não mostrar datas ou quantidades inventadas.
- **Duração:** 3–5 s.
- **Interrupções:** I0; se o serviço não fornecer evidência da próxima ocorrência, usar somente C36; erro ou nova edição cancela.
- **Saída:** S0 sem manipular prazo, lembrete ou concluir a nova ocorrência.
- **Sprites necessários:** CI-BASE; CI-RUNAS; CI-ASSENTIR; FX-RUNA.

### C38 — Assento breve acima de Nova Tarefa

- **Local e elemento real:** TarefasView / topo exterior do botão Nova Tarefa, somente se houver altura/largura aprovadas.
- **Gatilho:** D + G; botão permitido, fora de foco/hover; título/subtítulo e filtro não podem ocupar o volume superior.
- **Percurso:** Borda → lateral do botão → arco curto ao topo → descida pelo mesmo lado.
- **Ações:** Pensar, saltar, pousar, sentar com pernas fora do rótulo, olhar o ambiente, levantar e descer.
- **Variações:** Pausa de 3–5 s; olhar à esquerda/direita. Se faltar topo, escolher C04, sem encaixar à força.
- **Duração:** 12–19 s.
- **Interrupções:** I0; aproximação ou foco aciona C07; abrir formulário oculta imediatamente.
- **Saída:** S0 após descida segura, sem executar NovaTarefaCommand.
- **Sprites necessários:** CI-BASE; CI-PENSAR; CI-SALTAR; CI-POUSAR; CI-SENTAR/SENTADO/LEVANTAR-SENTADO; CI-OLHAR; CI-DESCER.

### C39 — Passeio pela lista de Lembretes

- **Local e elemento real:** CompromissosView / margem exterior em modo Lista, fora das linhas e filtros Próximos/Hoje/7 dias/Histórico.
- **Gatilho:** D + G; lista carregada, sem formulário e sem confirmação.
- **Percurso:** Borda → um ou dois pontos externos à lista → borda.
- **Ações:** Caminhar, consultar papel, observar o cabeçalho do filtro e guardar antes de sair.
- **Variações:** Filtros Próximos e 7 dias mudam o ponto elegível; não ler títulos, local ou horários.
- **Duração:** 10–16 s.
- **Interrupções:** I0; alterar modo/filtro, realizar/cancelar/reabrir ou receber novos dados remapeia.
- **Saída:** S0 sem interagir com ações das linhas.
- **Sprites necessários:** CI-BASE; CI-LER-ABRIR/LOOP/GUARDAR; CI-OLHAR.

### C40 — Pausa quando Hoje não tem resultados

- **Local e elemento real:** CompromissosView / exterior do estado Nada marcado com filtro Hoje em Lista.
- **Gatilho:** D + G; consulta atual sem erro e coleção do filtro vazia; não inferir ausência geral de compromissos.
- **Percurso:** Borda → bolsão lateral ao estado vazio → retorno.
- **Ações:** Olhar o próprio pergaminho, sentar se couber, descansar brevemente e guardar papel.
- **Variações:** Pausa em pé; pequeno bocejo; sem representação de lembretes cancelados como agenda livre.
- **Duração:** 9–15 s.
- **Interrupções:** I0; mudança de filtro/período, novo lembrete ou notificação real interrompe.
- **Saída:** S0 sem ocupar Novo lembrete ou a orientação do estado vazio.
- **Sprites necessários:** CI-BASE; CI-LER-ABRIR/LOOP/GUARDAR; CI-SENTAR/SENTADO/LEVANTAR-SENTADO; CI-BOCEJAR; CI-SALTAR; CI-POUSAR; CI-DESCER; CI-OLHAR.

### C41 — Leitura do histórico de Lembretes

- **Local e elemento real:** CompromissosView / margem externa do filtro Histórico, fora dos badges Realizado/Cancelado e ações de reabrir.
- **Gatilho:** D + G; filtro carregado e lista estável.
- **Percurso:** Borda → ponto de leitura externo → retorno.
- **Ações:** Consultar pergaminho, pausar, enrolar o papel com cuidado e sair andando.
- **Variações:** Olhar uma ou duas vezes; gesto neutro para registros realizados e cancelados.
- **Duração:** 10–16 s.
- **Interrupções:** I0; reabrir, editar, excluir, mudar filtro ou scroll encerra.
- **Saída:** S0 sem comemorar cancelamento nem restaurar compromisso.
- **Sprites necessários:** CI-BASE; CI-LER-ABRIR/LOOP/GUARDAR; CI-PENSAR.

### C42 — Ronda exterior da agenda semanal

- **Local e elemento real:** AgendaCalendarioView / margem fora do retângulo completo da grade Semana e dos cabeçalhos de dias.
- **Gatilho:** D + G; semana carregada, sem scroll/seleção e corredor exterior comprovadamente livre.
- **Percurso:** Borda → segmento do perímetro exterior → retorno; nenhuma travessia das células, mesmo vazias.
- **Ações:** Caminhar, observar a grade de longe, consultar papel e guardar.
- **Variações:** Lado exterior esquerdo/direito; percurso reduzido com semana densa; não usar faixas vazias de horário como chão.
- **Duração:** 8–14 s.
- **Interrupções:** I0; Hoje, setas de período, mudança de modo, dados, clique numa faixa ou altura da grade invalida.
- **Saída:** S0 sem passar por etiquetas, sinais de conflito ou células clicáveis.
- **Sprites necessários:** CI-BASE; CI-OLHAR; CI-LER-ABRIR/LOOP/GUARDAR.

### C43 — Descanso fora da agenda mensal

- **Local e elemento real:** AgendaCalendarioView / bolsão exterior ao painel Mês, distante das células, números, etiquetas e ExtrasTexto.
- **Gatilho:** D + G; mês estável e área externa com volume de repouso; nenhum apoio dentro da grade.
- **Percurso:** Borda → apoio exterior aprovado do painel → mesma borda.
- **Ações:** Parar, sentar acima de borda permitida ou ficar em pé, observar, espreguiçar e sair.
- **Variações:** Sentado somente se nenhum membro cobrir célula/título; em pé quando houver apenas área de espera.
- **Duração:** 11–18 s.
- **Interrupções:** I0; navegação de mês, Hoje, clique em dia, tooltip ou novo item interrompe.
- **Saída:** S0 por descida exterior segura; sem saltar entre dias.
- **Sprites necessários:** CI-BASE; CI-SENTAR/SENTADO/LEVANTAR-SENTADO; CI-ESPREGUICAR; CI-OLHAR; CI-DESCER; CI-SALTAR; CI-POUSAR.

### C44 — Atenção discreta a choque de horários

- **Local e elemento real:** AgendaCalendarioView / ponto exterior à grade, sem sobrepor o aviso âmbar de conflito.
- **Gatilho:** R contextual + G; montagem da agenda atual confirma conflitos; no máximo uma reação por mudança relevante do conjunto, com correlação futura.
- **Percurso:** Ponto exterior previamente válido → recuo para a borda; sem ir ao compromisso.
- **Ações:** Parar de caminhar, olhar para a agenda e adotar expressão atenta; não sugerir que resolveu o choque.
- **Variações:** Assentimento sério ou mão no queixo; RE-ATENTO apenas com P e slot futuro reservado já validado; sem slot, oculto no modo de movimento reduzido.
- **Duração:** 2–3 s.
- **Interrupções:** I0; editar um compromisso, abrir formulário ou interagir com o aviso elimina a reação.
- **Saída:** S0; não reenfileirar a cada montagem ou scroll da mesma semana.
- **Sprites necessários:** CI-BASE; CI-ATENCAO; CI-PENSAR; CI-OLHAR; CI-ASSENTIR; RE-ATENTO.

### C45 — Pergaminho que espera uma data

- **Local e elemento real:** AgendaCalendarioView / lateral exterior da faixa Sem data marcada e botão Ver na lista.
- **Gatilho:** D + G; TotalSemData positivo e faixa realmente visível; layout e texto da faixa protegidos.
- **Percurso:** Borda → ponto de observação exterior → borda.
- **Ações:** Abrir papel próprio, olhar para a faixa, pensar e guardar; expectativa neutra.
- **Variações:** Um olhar ou leitura breve; sem indicar vencimento ou envio de notificação para lembrete sem data.
- **Duração:** 7–11 s.
- **Interrupções:** I0; Ver na lista, mudança da contagem, formulário ou perda de espaço encerra.
- **Saída:** S0 sem executar navegação e sem usar a faixa como plataforma.
- **Sprites necessários:** CI-BASE; CI-LER-ABRIR/LOOP/GUARDAR; CI-PENSAR.

### C46 — Exploração da hierarquia de categorias

- **Local e elemento real:** CategoriasView / corredor exterior de cartões de categoria pai e subcategorias.
- **Gatilho:** D + G; lista carregada e estável, sem formulário ou confirmação.
- **Percurso:** Borda → observação externa do conjunto pai/filhos → retorno; não percorrer o recuo interno das subcategorias.
- **Ações:** Olhar o grupo, alinhar duas runas na mão como coleção, pensar e recolher.
- **Variações:** Observar categoria sem filhos ou com filhos; escolher só cartões visíveis; sem interpretar nomes.
- **Duração:** 9–15 s.
- **Interrupções:** I0; nova subcategoria, editar, excluir, scroll ou crescimento do cartão invalida.
- **Saída:** S0 sem reorganizar a árvore nem imitar inclusão.
- **Sprites necessários:** CI-BASE; CI-OLHAR; CI-RUNAS; CI-PENSAR.

### C47 — Runas diante de Categorias vazias

- **Local e elemento real:** CategoriasView / exterior do estado Nenhuma categoria criada.
- **Gatilho:** D + G; carregamento concluído sem falha e ausência de categorias confirmada.
- **Percurso:** Borda → ponto amplo ao lado do bloco vazio → borda.
- **Ações:** Tirar duas pedras rúnicas do estojo, alinhá-las na própria mão, conferir e guardar.
- **Variações:** Ordem das duas pedras; pausa de pensamento; nunca deixá-las persistidas sobre a UI.
- **Duração:** 9–14 s.
- **Interrupções:** I0; Nova Categoria, nova consulta ou chegada de dados encerra.
- **Saída:** S0 sem esconder a orientação para criar categoria.
- **Sprites necessários:** CI-BASE; CI-RUNAS; CI-PENSAR; FX-RUNA.

### C48 — Categoria registrada

- **Local e elemento real:** CategoriasView / margem externa após SalvarCategoria fechar o formulário e atualizar lista.
- **Gatilho:** R + G; resultado de salvamento confirmado pelo domínio, com evento futuro correlacionado; criação/edição não inferidas por texto da mensagem.
- **Percurso:** Entrada curta → pausa fora do toast → saída.
- **Ações:** Guardar pequena runa no estojo e assentir.
- **Variações:** Criação de raiz/subcategoria ou edição com mesmo gesto discreto; sem celebração por abrir formulário.
- **Duração:** 2–4 s.
- **Interrupções:** I0; falha, formulário ainda aberto ou próxima ação descarta.
- **Saída:** S0 sem visitar o nome da categoria nem executar comandos de linha.
- **Sprites necessários:** CI-BASE; CI-RUNAS; CI-SUCESSO; CI-ASSENTIR.

### C49 — Sesta acima de Nova Categoria

- **Local e elemento real:** CategoriasView / topo exterior do botão Nova Categoria, apenas se volume deitado completo estiver aprovado.
- **Gatilho:** D + G; ociosidade longa, botão permitido e distante de foco/hover; arco e saída disponíveis.
- **Percurso:** Borda → lateral → salto ao topo → descida exterior → saída.
- **Ações:** Examinar, saltar/pousar, deitar por poucos segundos, bocejar, levantar e descer.
- **Variações:** Deitado curto ou sentado se a variante inteira couber; botão sem espaço recebe apenas C04, não esta cena.
- **Duração:** 16–25 s; repouso 4–7 s.
- **Interrupções:** I0; aproximação do usuário aciona retirada; formulário oculta imediatamente.
- **Saída:** S0 após levantar e descer com quadros próprios; nenhuma rotação rígida do sprite.
- **Sprites necessários:** CI-BASE; CI-PENSAR; CI-SALTAR; CI-POUSAR; CI-DEITAR/DEITADO/LEVANTAR-DEITADO; CI-SENTAR/SENTADO/LEVANTAR-SENTADO; CI-BOCEJAR; CI-DESCER.

### C50 — Conferência de runas em Variáveis Globais

- **Local e elemento real:** VariaveisGlobaisView / corredor exterior à lista; nomes entre chaves, valores e descrições são áreas protegidas.
- **Gatilho:** D + G; lista carregada, sem formulário e sem confirmação de exclusão.
- **Percurso:** Borda → ponto de leitura exterior → borda.
- **Ações:** Abrir pergaminho com grafismos fictícios entre dois sinais, pensar, guardar e seguir.
- **Variações:** Olhar o próprio papel ou preparar a pena sem magia; não copiar valores de variáveis.
- **Duração:** 10–16 s.
- **Interrupções:** I0; edição, exclusão, avisos de uso ou atualização da lista interrompem.
- **Saída:** S0 sem revelar conteúdo de valores nem modificar variável.
- **Sprites necessários:** CI-BASE; CI-LER-ABRIR/LOOP/GUARDAR; CI-PENSAR.

### C51 — Variável salva

- **Local e elemento real:** VariaveisGlobaisView / margem externa após SalvarVariavel e fechamento do formulário.
- **Gatilho:** R + G; salvamento confirmado e recarga concluída; uso futuro do resultado tipado da operação.
- **Percurso:** Entrada curta → ponto seguro fora da mensagem → mesma borda.
- **Ações:** Retirar e abrir parcialmente o próprio pergaminho, fechá-lo, guardá-lo e assentir.
- **Variações:** Criação ou atualização; sem animar alteração de macros que apenas reutilizam essa variável.
- **Duração:** 2–4 s.
- **Interrupções:** I0; FormErro, formulário aberto ou aviso de exclusão suspende; nenhuma reação por nome duplicado.
- **Saída:** S0 sem simular preenchimento ou disparar uma macro.
- **Sprites necessários:** CI-BASE; CI-LER-GUARDAR; CI-SUCESSO; CI-ASSENTIR; CI-LER-ABRIR.

### C52 — Atenção a lembrete realmente emitido

- **Local e elemento real:** MainWindow / bolsão exterior da tela ativa, fora de dados e controles; não desenhar sobre a notificação do Windows.
- **Gatilho:** R + G; evento real de lembrete vencido ou aviso de compromisso emitido pelo serviço, com janela principal ativa e espaço livre; notificação continua independente.
- **Percurso:** Preferir personagem já parado em área válida; no máximo entrada curta pela margem.
- **Ações:** Parar a ação decorativa, olhar atento, levar mão ao ouvido e recolher; sem reproduzir título ou conteúdo do lembrete.
- **Variações:** Gesto de atenção curto ou RE-ATENTO em slot futuro; se principal oculta ou editor ativo, sem mascote.
- **Duração:** 1,5–3 s.
- **Interrupções:** I0; clicar na notificação, abrir tarefa/compromisso, receber novo aviso ou perder foco encerra; não manter fila por lembrete.
- **Saída:** S0 ou ocultação; nunca substituir notificação por animação.
- **Sprites necessários:** CI-BASE; CI-ATENCAO; CI-OLHAR; RE-ATENTO.

### C53 — Observar uma mudança de aparência

- **Local e elemento real:** ConfiguracoesView / margem exterior ao painel Aparência, fora dos cartões Sistema/Claro/Escuro e paleta.
- **Gatilho:** D contextual + G; alteração de tema pelo usuário já aplicada e layout estabilizado; cenas não disparam em cada amostra de cor.
- **Percurso:** Borda → ponto de observação exterior → borda.
- **Ações:** Olhar o próprio elmo e a capa, acomodar postura e assentir; materiais do personagem mantêm identidade.
- **Variações:** Pequena observação de contraste ou gesto neutro; não recolorir automaticamente o personagem com cada accent.
- **Duração:** 5–8 s.
- **Interrupções:** I0; nova troca de tema/cor, captura de atalho, foco ou scroll encerra.
- **Saída:** S0 após remapeamento; sem executar AplicarTemaCommand ou seleção de cor.
- **Sprites necessários:** CI-BASE; CI-OLHAR; CI-ASSENTIR.

### C54 — Inspeção decorativa de Saúde do App

- **Local e elemento real:** ConfiguracoesView / exterior do painel Saúde do App, distante de memória, versão, CPU, disco e Atualizar.
- **Gatilho:** D + G; painel visível e estável, sem coleta/atualização pendente relevante.
- **Percurso:** Borda → ponto de leitura exterior ao painel → retorno.
- **Ações:** Abrir papel, observar o painel e pensar, sem transformar números em diagnóstico.
- **Variações:** Leitura breve ou olhar de lado; sem runa vermelha, julgamento de saúde ou alegação de otimização.
- **Duração:** 7–12 s.
- **Interrupções:** I0; Atualizar, scroll, erro de coleta ou mudança de layout interrompe.
- **Saída:** S0 sem alterar recursos ou iniciar manutenção.
- **Sprites necessários:** CI-BASE; CI-LER-ABRIR/LOOP/GUARDAR; CI-PENSAR.

### C55 — Preferências salvas

- **Local e elemento real:** ConfiguracoesView / bolsão externo ao painel e mensagem, após Salvar configurações.
- **Gatilho:** R + G; persistência concluída sem falha e evento futuro correlacionado ao comando; trocar tema em tempo real não prova que todo formulário foi salvo.
- **Percurso:** Entrada curta → pausa fora do botão Salvar configurações → borda.
- **Ações:** Retirar e abrir parcialmente um pergaminho fictício de ajustes, fechá-lo, guardar, assentir e sair.
- **Variações:** Confirmação com cabeça ou pena recolhida; sem reação para Restaurar padrão antes de seu resultado.
- **Duração:** 2–4 s.
- **Interrupções:** I0; erro, captura de atalho, novo ajuste ou navegação elimina reação.
- **Saída:** S0 sem abrir notificação de teste nem mudar configurações.
- **Sprites necessários:** CI-BASE; CI-LER-GUARDAR; CI-SUCESSO; CI-LER-ABRIR; CI-ASSENTIR.

### C56 — Leitura junto a uma pergunta frequente

- **Local e elemento real:** AjudaView / margem exterior da coluna de FAQ, após expansão de uma resposta.
- **Gatilho:** D contextual + G; FAQ aberta estável, ociosidade e espaço fora da resposta; não existe busca funcional nessa tela.
- **Percurso:** Borda → bolsão de leitura exterior ao cartão → retorno.
- **Ações:** Consultar o próprio pergaminho, olhar a FAQ de longe, pensar e guardar.
- **Variações:** FAQ aberta ou fechada muda o local elegível; nenhuma circulação dentro do texto ou sobre o ToggleButton.
- **Duração:** 10–16 s.
- **Interrupções:** I0; expandir/recolher pergunta, scroll ou seleção invalida; recalcular antes de nova cena.
- **Saída:** S0 sem abrir/fechar perguntas e sem encobrir respostas.
- **Sprites necessários:** CI-BASE; CI-LER-ABRIR/LOOP/GUARDAR; CI-PENSAR.

### C57 — Aprendiz de atalhos

- **Local e elemento real:** AjudaView / exterior do cartão Atalhos úteis, sem ocupar chips Ctrl+Espaço, Ctrl+Alt+1…9, /atalho e Esc.
- **Gatilho:** D + G; cartão visível, sem transição/scroll e topo ou lateral exterior aprovados.
- **Percurso:** Borda → ponto lateral → apoio superior somente se corpo todo couber → descida → borda.
- **Ações:** Observar os chips, consultar uma runa no próprio papel, assentir e guardar.
- **Variações:** Em pé ou sentado acima do cartão; não fingir apertar tecla funcional ou gerar entrada.
- **Duração:** 9–15 s.
- **Interrupções:** I0; uso real de atalho, popup ou navegação oculta; nenhuma leitura dos dados abertos pelo atalho.
- **Saída:** S0 sem disparar buscador, macro ou Esc.
- **Sprites necessários:** CI-BASE; CI-OLHAR; CI-LER-ABRIR/LOOP/GUARDAR; CI-ASSENTIR; CI-SENTAR/SENTADO/LEVANTAR-SENTADO; CI-DESCER; CI-SALTAR; CI-POUSAR.

### C58 — Liberar a consulta do Manual SQL

- **Local e elemento real:** MainWindow ao navegar para ManualSqlView; WebView e AvisoArquivoAusente inteiramente protegidos.
- **Gatilho:** I; página Manual SQL ativada, independentemente de recurso disponível, ausente ou navegação web.
- **Percurso:** Nenhum percurso sobre o WebView2; retirar na tela anterior se possível, caso contrário ocultar.
- **Ações:** Recolher objeto/efeito e suspender personagem. Esta é uma cena de transição, sem exploração dentro do manual.
- **Variações:** Saída curta da margem anterior ou ocultação direta; nenhuma reação offline por ausência de arquivo local.
- **Duração:** 0–1 s para retirada; oculto enquanto página ativa nesta proposta.
- **Interrupções:** Troca imediata prevalece; não esperar carregamento web para liberar conteúdo.
- **Saída:** Reaparecimento só após outra página elegível e novo G; sobreposição web depende de avaliação futura própria.
- **Sprites necessários:** CI-BASE; CI-LER-GUARDAR; CI-MAGIA-SAIR.

### C59 — Do passeio para a mesa de trabalho

- **Local e elemento real:** DashboardView / futura área reservada de vinheta externa ao conteúdo do cartão de saudação; nenhum slot existente presumido.
- **Gatilho:** P + D + G; slot de mesa previamente aprovado, corpo fora de cena e contexto estável.
- **Percurso:** Corpo termina percurso na borda e desaparece; depois vinheta aparece em slot distinto, sem mesa deslizando pelo painel.
- **Ações:** Finalizar caminhada, recolher objetos, sair; ME-ENTRAR → ME-B0 → escrita suave → breve caneca → ME-SAIR.
- **Variações:** ME-THINK em vez de caneca; saudação de mesa somente se saudação global ainda não ocorreu; uma única família visível por vez.
- **Duração:** 12–20 s; escrita em passagens de 2 s com pausas de 1–3 s, limitada nesta visita.
- **Interrupções:** I0; qualquer navegação, formulário ou perda de slot remove a vinheta e cancela a transição.
- **Saída:** ME-B0 → ME-SAIR; mesa não vira corpo instantaneamente no mesmo ponto.
- **Sprites necessários:** CI-BASE; ME-ENTRAR/SAIR; ME-B0; ME-WORK; ME-COFFEE; ME-THINK; ME-GREET.

### C60 — Presença estática com movimento reduzido

- **Local e elemento real:** Futuro slot decorativo reservado na janela principal ou em estado vazio aprovado; fora de rótulos, controles e área web.
- **Gatilho:** P; preferência futura de movimento reduzido habilitada, ou opção explícita de presença estática; ocultar se nenhum slot couber.
- **Percurso:** Nenhum deslocamento, salto, corrida ou transição entre apoios.
- **Ações:** Mostrar RE-NEUTRO ou RE-LENDO em pose fixa; sem partículas, piscadas repetidas ou ciclos de respiração.
- **Variações:** Retrato neutro/lendo por contexto elegível; atenção estática apenas para evento real e sem substituir sua mensagem.
- **Duração:** Enquanto contexto/slot forem válidos e opção estiver ativa; nenhuma animação contínua.
- **Interrupções:** I0; foco, modal, edição, indisponibilidade de espaço ou escolha Ocultar remove imediatamente.
- **Saída:** Ocultação direta; não agendar cenas de exploração invisíveis para executar ao reativar.
- **Sprites necessários:** RE-NEUTRO; RE-LENDO; RE-ATENTO.

## 5. Registro de cobertura e de estados protegidos

**Resultado desta etapa: análise estática concluída para o planejamento; validação funcional/visual das cenas não executada.** Nenhuma ficha está aprovada para reprodução em produção ou homologação. A leitura do código verifica a existência de componentes e condições; não comprova encaixe, movimento, legibilidade, desempenho ou operação correta em runtime.

| Tela ou superfície | Estados considerados pela leitura | Cenas / política | Pendência para validação real |
| --- | --- | --- | --- |
| MainWindow | Navegação, submenu de Macros, avatar, troca de CurrentView e perda de contexto. | C01–C08, C52, C60. | Medir área cliente e corredores em cada tamanho/DPI; verificar foco, bandeja, múltiplas janelas e instância única. |
| Início | Métricas, ranking cheio/vazio, tarefas presentes/ausentes, compromissos do dia e conclusão. | C09–C15, C59. | Dados sintéticos para cada combinação; nenhum slot de mesa existente confirmado. |
| Macros | Lista, busca, favoritos, arquivadas, categoria; criação/edição, cópia e importação/exportação. | C16–C24; formulário/histórico protegidos. | Medir filtros/lista; validar resultados tipados, cancelamentos, duplicidade, arquivo inválido e falha de cópia sem animação falsa. |
| Notas | Lista/fixadas, busca sem resultados, ausência global condicional, editor, leitura, formatação, seleção, cópia e gravação. | C25–C32. Editor inteiro protegido. | Distinguir consulta antiga/atual, erro/loading, zero resultados e primeira nota persistida; testar conteúdo longo e popups de formatação. |
| Tarefas | Abertas/Hoje/Atrasadas/Concluídas, recorrência, formulário, confirmação e resultado de conclusão. | C33–C38. | Dados de limites de data, recorrência e reabertura; confirmar próxima ocorrência sem reação duplicada. |
| Lembretes | Lista com filtros, Semana/Mês, vazio filtrado, sem data, conflitos, realizado/cancelado e formulário. | C39–C45; células de agenda protegidas mesmo vazias. | Semana densa, mês com extras, horários extremos, conflito, scroll e calendário de seleção abertos; medir perímetro externo. |
| Categorias | Raiz/subcategorias, vazio, criação/edição e confirmação de exclusão com aviso de uso. | C46–C49; confirmação protegida. | Árvores grandes, nomes longos, categoria usada e alterações de altura; topo de Nova Categoria continua candidato. |
| Variáveis Globais | Lista, criação/edição, valor/descrição e confirmação de exclusão com uso em macros. | C50–C51; formulário/confirmação protegidos. | Valores longos e vazios, duplicidade, atualização e falha; não transportar valores para lógica do mascote. |
| Configurações | Aparência, Personalização, Comportamento, Gatilho, atalhos, Saúde do App e salvamento. | C53–C55; captura de atalho protegida. | Tema claro/escuro/Sistema, cores, scroll e erro de persistência; preferências de mascote ainda não existem. |
| Ajuda | FAQ aberta/fechada e Atalhos úteis; Buscar na ajuda é elemento estático. | C56–C57. | Expansão com resposta longa, largura mínima e scroll; não inventar eventos de busca. |
| Manual SQL | WebView2 e aviso de arquivo ausente identificados no código. | C58; oculto em toda a página nesta proposta. | Inspeção do HTML em navegador não valida composição WPF/WebView2; integração visual web permanece fora do recorte. |
| Buscador rápido | Cinco abas: Macros, Tarefas, Lembretes, Notas e Copiados; lista, busca, preview e comandos. | Sem circulação e sem cópia do personagem; principal oculta/desativada suspende. | Percorrer abas com mouse/teclado, itens vazios/cheios e preview; não deslocar focus ou prejudicar operação rápida. |
| MacroPopupWindow e VariaveisWindow | Sugestão de macro e entrada dos valores necessários à inserção. | Protegidos; nenhuma cena local. | Digitação, seleção, preenchimento/cancelamento e janela de destino; não gerar reação de erro em cancelamento. |
| NotaFlutuanteWindow | Criação, edição, leitura, auto salvamento e formatação. | Protegida; sem mesa nem corpo. | Texto extenso, seleção, troca de nota, foco e fechamento; não usar saída da janela como evidência de sucesso. |
| TarefaFlutuanteWindow / CompromissoFlutuanteWindow | Edição e ações rápidas. | Protegidas; sem circulação. | Validar formulário, datas, adiar/concluir e confirmação com dados sintéticos. |
| TourWindow, diálogos de arquivo, calendários, menus e confirmações | Abertura de orientação, importação/exportação, escolha de data e ações explícitas. | Suspender exploração; nenhuma brincadeira com Confirmar, Excluir, Restaurar ou Sair. | Observar todas as camadas temporárias e sua liberação; abertura nunca espera animação. |
| Página pessoal oculta | Existência e rota de acionamento registradas no mapa do sistema. | Excluída do planejamento de cenas de produto; mascote suspenso nesse contexto. | Nenhuma inspeção de conteúdo pessoal necessária para esta integração. |
| Processamento/inserção de macro | Inserção envolve resolução de conteúdo, possível diálogo e envio ao programa de destino. | Sem cena ativa enquanto principal desativada, popup ou diálogo aberto. Reações expiradas são descartadas; sucesso de envio não confirma ação do programa externo. | Contrato futuro correlacionado pode permitir reação curta apenas quando principal continua elegível; não criar loop de processamento decorativo nem alongar operação. |

### Situações do briefing ainda condicionais ou sem correspondência

- Login, sessão expirada, permissões de equipe, sincronização, API remota, IA/ditado, macro agendada e execução de macros em lote com pausa/retomada não foram tratadas como funcionalidades atuais do aplicativo.
- Sem evento real correspondente, não ativar offline_enter/idle/exit, bug_fix, shield_warn, dragon_reward ou uma reação de marco inventada. O aplicativo funcionar sem Internet não é falha de conexão.
- As cenas de busca usam estado de consulta concluída; não simulam processamento só para mostrar conjuração. Capturar um resultado atual é trabalho futuro do adaptador de eventos, sem analisar strings de Mensagem.
- O briefing é mais amplo que este catálogo: não se afirma que todos os seus 120 contextos estejam presentes no produto ou detalhados aqui.

## 6. Ordem proposta e critérios para tornar uma cena elegível

1. **Medir antes de produzir todo o catálogo:** em uma execução local isolada, medir primeiro Início, lista de Macros e lista de Notas com dados sintéticos. Confirmar ao menos uma borda de entrada, um ponto de pausa e uma saída para a família CI. Se só houver espaço para retrato, esse será o limite daquele layout.
2. **Provar prioridade do usuário:** protótipo futuro de C02/C03/C07/C08, com instância única, movimento reduzido, foco preservado e retirada em navegação/scroll/menu/modal. O primeiro aceite deve demonstrar ausência de comando disparado pelo mascote.
3. **Calibrar ciclos reais:** validar caminhada em ambos os lados, parada, giro, passada/velocidade, retirada e papel. Depois validar corrida, salto completo, altura permitida, pouso, sentar, deitar e levantar.
4. **Liberar somente apoios medidos:** começar por margens e laterais. Topos de botões/cartões de C01, C11, C38 e C49 exigem volume acima e saída segura; a inexistência desse espaço não justifica refazer a tela sem demanda específica.
5. **Adicionar reações reais:** correlacionar resultado e contexto de salvamento/cópia/conclusão, expiração e não duplicação. C29 e C37 dependem de evidências adicionais de marco/recorrência; até lá permanecem inelegíveis.
6. **Integrar famílias de mesa/retrato:** somente depois de um slot reservado ser aprovado sem comprometer conteúdo. Revisar ME-B0/ME-BN e as ligações; C59 garante continuidade entre famílias.
7. **Executar C01 completa:** só depois de todos os trechos e movimentos terem sido validados isoladamente no mesmo layout. Variações por Nova macro, Nova Nota, Nova Tarefa e Nova Categoria dependem de aprovação individual do apoio; não herdam a aprovação de outro botão.

### Checklist de QA futuro (não executado nesta etapa)

| Critério | Resultado esperado para aceite |
| --- | --- |
| Mouse e teclado | Clique, Tab, Enter, atalhos, seleção, edição e scroll funcionam sem espera ou foco roubado; personagem não dispara comandos. |
| Caminho completo | Nenhum quadro de corpo, papel, capa, mão, sombra ou magia invade título, rótulo, dado, campo ou controle protegido. |
| Apoio dinâmico | Scroll, reordenação, resize, DPI e mudança de modo não deixam âncora em item removido; retirada ou ocultação resolve falta de saída. |
| Acoplamento ao estado | Loading, falha, busca vazia e cancelamento não viram ausência global, primeira nota ou sucesso. Resultado de operação antiga não é animado na tela nova. |
| Continuidade | Objetos são retirados e guardados; personagem para antes de ler/deitar; levanta antes de correr; mesa nunca acompanha caminhada. |
| Variações | Todas as direções e variações têm sprites e envelopes próprios; não há espelhamento que altere identidade nem caminhada acelerada como corrida. |
| Visibilidade | Uma instância, nenhuma animação em janela minimizada/desativada ou atrás de modal; sem fila acumulada durante pausa. |
| Acessibilidade | Movimento reduzido funciona sem locomoção ou loops; ocultar realmente suspende o planejamento; nenhuma redução ilegível para caber. |
| Arte e desempenho | Pixel art legível nos temas, escala correta, sem recortes e sem atrasar interação; orçamento de atualização/CPU medido no aplicativo real. |
| Regressão | Navegação, gravação, cópia, importação/exportação, tarefas, lembretes e edição de notas preservam resultados e mensagens; mascote apenas observa eventos. |

**Pendência geral:** faltam sprites finais, controlador, registro de apoios, geometria observada, fixtures locais e execução das cenas. Este documento entrega mapeamento e coreografia revisáveis; não autoriza implementação, alteração de layout, publicação, deploy, commit ou operações no banco pessoal.
