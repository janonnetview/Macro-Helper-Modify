# Evidências do diagnóstico espacial SKald

[Relatório principal](../SKald-validacao-espacial.md) · [Resultados finais](resumo-execucao.json) · [Fontes e integridade](fontes-verificadas.json)

Capturas de WPF real com dados sintéticos, em fonte oculta e a 96 DPI. Nenhuma delas é captura da janela pessoal. São 96 PNGs: 40 estados de base, 20 após rolagem, dez quadros temporais mais uma referência sem contornos, 20 estados de interrupções e cinco de retirada/resize.

O retângulo é um envelope de teste de 64 DIP, com folga de 8 DIP. Contornos são instrumentação de diagnóstico. Não há desenho ou animação final de personagem.

**Resultado:** build sem avisos/erros; 499 testes aprovados, dos quais 46 pertencem ao diagnóstico. As regiões classificadas como rejeitadas são conclusões esperadas da medição, não testes com falha.

## Sequencia temporal R01

Notas, uma nota, tema escuro, janela 1200 × 760. [Referência sem contornos](R01-referencia-sem-contornos.png). A referência mantém internamente a última posição de diagnóstico, mas oculta a camada inteira; seu JSON não deve ser lido como um quadro com corpo visível.

Relógio virtual de 0 a 16 segundos. Os gestos de papel estão apenas nomeados para reservar o intervalo futuro. A sequência comprova posições e retirada de um retângulo; não comprova marcha, pés, pergaminho ou fluidez de sprites.

| Tempo | Quadro | Dados |
| --- | --- | --- |
| 0 s | [antes](R01-00-00s-antes.png) | [JSON](R01-00-00s-antes.json) |
| 1 s | [entrada-local](R01-01-01s-entrada-local.png) | [JSON](R01-01-01s-entrada-local.json) |
| 2 s | [andar](R01-02-02s-andar.png) | [JSON](R01-02-02s-andar.json) |
| 4 s | [parar](R01-03-04s-parar.png) | [JSON](R01-03-04s-parar.json) |
| 6 s | [abrir-papel-proposto](R01-04-06s-abrir-papel-proposto.png) | [JSON](R01-04-06s-abrir-papel-proposto.json) |
| 9 s | [ler-proposto](R01-05-09s-ler-proposto.png) | [JSON](R01-05-09s-ler-proposto.json) |
| 12 s | [guardar-proposto](R01-06-12s-guardar-proposto.png) | [JSON](R01-06-12s-guardar-proposto.json) |
| 14 s | [sair](R01-07-14s-sair.png) | [JSON](R01-07-14s-sair.json) |
| 15 s | [fim-do-percurso](R01-08-15s-fim-do-percurso.png) | [JSON](R01-08-15s-fim-do-percurso.json) |
| 16 s | [oculto](R01-09-16s-oculto.png) | [JSON](R01-09-16s-oculto.json) |

[Manifesto temporal](R01-sequencia.json). Início `(283,663)`, pausa `(403,663)`, retorno ao início; corpo 64 × 64; união com folga `(275,655,200,80)`.

## Matriz de base

Os nomes indicam tela, largura, tema e quantidade inicial. Altura: 760 para largura 1200; 600 para largura 940. Configurações usa contagens, e Início respeita os cortes descritos no relatório. As medidas são `x,y,largura,altura` da raiz WPF em DIP. “Input” conta pontos em controles visíveis cujo hit test permaneceu igual.

| Captura | Medições | Região | Candidato (DIP) | Input |
| --- | --- | --- | --- | --- |
| [Categorias-1200-Dark-00](Categorias-1200-Dark-00.png) | [JSON](Categorias-1200-Dark-00.json) | Aprovada no ensaio | 275,456,883,279 | 1 |
| [Categorias-1200-Dark-01](Categorias-1200-Dark-01.png) | [JSON](Categorias-1200-Dark-01.json) | Aprovada no ensaio | 275,341,883,394 | 6 |
| [Categorias-1200-Dark-18](Categorias-1200-Dark-18.png) | [JSON](Categorias-1200-Dark-18.json) | Rejeitada para 64 + folga | 263,168,0,0 | 24 |
| [Categorias-1200-Light-00](Categorias-1200-Light-00.png) | [JSON](Categorias-1200-Light-00.json) | Aprovada no ensaio | 275,456,883,279 | 1 |
| [Categorias-1200-Light-01](Categorias-1200-Light-01.png) | [JSON](Categorias-1200-Light-01.json) | Aprovada no ensaio | 275,341,883,394 | 6 |
| [Categorias-1200-Light-18](Categorias-1200-Light-18.png) | [JSON](Categorias-1200-Light-18.json) | Rejeitada para 64 + folga | 263,168,0,0 | 24 |
| [Categorias-940-Dark-00](Categorias-940-Dark-00.png) | [JSON](Categorias-940-Dark-00.json) | Aprovada no ensaio | 275,456,623,119 | 1 |
| [Categorias-940-Dark-01](Categorias-940-Dark-01.png) | [JSON](Categorias-940-Dark-01.json) | Aprovada no ensaio | 275,341,623,234 | 6 |
| [Categorias-940-Dark-18](Categorias-940-Dark-18.png) | [JSON](Categorias-940-Dark-18.json) | Rejeitada para 64 + folga | 263,168,0,0 | 16 |
| [Categorias-940-Light-00](Categorias-940-Light-00.png) | [JSON](Categorias-940-Light-00.json) | Aprovada no ensaio | 275,456,623,119 | 1 |
| [Categorias-940-Light-01](Categorias-940-Light-01.png) | [JSON](Categorias-940-Light-01.json) | Aprovada no ensaio | 275,341,623,234 | 6 |
| [Categorias-940-Light-18](Categorias-940-Light-18.png) | [JSON](Categorias-940-Light-18.json) | Rejeitada para 64 + folga | 263,168,0,0 | 16 |
| [Configuracoes-1200-Dark-01](Configuracoes-1200-Dark-01.png) | [JSON](Configuracoes-1200-Dark-01.json) | Aprovada no ensaio | 275,78,162,657 | 17 |
| [Configuracoes-1200-Dark-18](Configuracoes-1200-Dark-18.png) | [JSON](Configuracoes-1200-Dark-18.json) | Aprovada no ensaio | 275,78,162,657 | 17 |
| [Configuracoes-1200-Light-01](Configuracoes-1200-Light-01.png) | [JSON](Configuracoes-1200-Light-01.json) | Aprovada no ensaio | 275,78,162,657 | 17 |
| [Configuracoes-1200-Light-18](Configuracoes-1200-Light-18.png) | [JSON](Configuracoes-1200-Light-18.json) | Aprovada no ensaio | 275,78,162,657 | 17 |
| [Configuracoes-940-Dark-01](Configuracoes-940-Dark-01.png) | [JSON](Configuracoes-940-Dark-01.json) | Rejeitada para 64 + folga | 275,78,32,497 | 16 |
| [Configuracoes-940-Dark-18](Configuracoes-940-Dark-18.png) | [JSON](Configuracoes-940-Dark-18.json) | Rejeitada para 64 + folga | 275,78,32,497 | 16 |
| [Configuracoes-940-Light-01](Configuracoes-940-Light-01.png) | [JSON](Configuracoes-940-Light-01.json) | Rejeitada para 64 + folga | 275,78,32,497 | 16 |
| [Configuracoes-940-Light-18](Configuracoes-940-Light-18.png) | [JSON](Configuracoes-940-Light-18.json) | Rejeitada para 64 + folga | 275,78,32,497 | 16 |
| [Dashboard-1200-Dark-01](Dashboard-1200-Dark-01.png) | [JSON](Dashboard-1200-Dark-01.json) | Rejeitada para 64 + folga | 507,100,460,61 | 3 |
| [Dashboard-1200-Dark-18](Dashboard-1200-Dark-18.png) | [JSON](Dashboard-1200-Dark-18.json) | Rejeitada para 64 + folga | 507,100,460,61 | 2 |
| [Dashboard-1200-Light-01](Dashboard-1200-Light-01.png) | [JSON](Dashboard-1200-Light-01.json) | Rejeitada para 64 + folga | 507,100,460,61 | 3 |
| [Dashboard-1200-Light-18](Dashboard-1200-Light-18.png) | [JSON](Dashboard-1200-Light-18.json) | Rejeitada para 64 + folga | 507,100,460,61 | 2 |
| [Dashboard-940-Dark-01](Dashboard-940-Dark-01.png) | [JSON](Dashboard-940-Dark-01.json) | Rejeitada para 64 + folga | 683,100,187,65 | 3 |
| [Dashboard-940-Dark-18](Dashboard-940-Dark-18.png) | [JSON](Dashboard-940-Dark-18.json) | Rejeitada para 64 + folga | 683,100,187,65 | 2 |
| [Dashboard-940-Light-01](Dashboard-940-Light-01.png) | [JSON](Dashboard-940-Light-01.json) | Rejeitada para 64 + folga | 683,100,187,65 | 3 |
| [Dashboard-940-Light-18](Dashboard-940-Light-18.png) | [JSON](Dashboard-940-Light-18.json) | Rejeitada para 64 + folga | 683,100,187,65 | 2 |
| [Notas-1200-Dark-00](Notas-1200-Dark-00.png) | [JSON](Notas-1200-Dark-00.json) | Aprovada no ensaio | 275,447,883,288 | 2 |
| [Notas-1200-Dark-01](Notas-1200-Dark-01.png) | [JSON](Notas-1200-Dark-01.json) | Aprovada no ensaio | 275,355,883,380 | 7 |
| [Notas-1200-Dark-18](Notas-1200-Dark-18.png) | [JSON](Notas-1200-Dark-18.json) | Rejeitada para 64 + folga | 263,224,0,0 | 32 |
| [Notas-1200-Light-00](Notas-1200-Light-00.png) | [JSON](Notas-1200-Light-00.json) | Aprovada no ensaio | 275,447,883,288 | 2 |
| [Notas-1200-Light-01](Notas-1200-Light-01.png) | [JSON](Notas-1200-Light-01.json) | Aprovada no ensaio | 275,355,883,380 | 7 |
| [Notas-1200-Light-18](Notas-1200-Light-18.png) | [JSON](Notas-1200-Light-18.json) | Rejeitada para 64 + folga | 263,224,0,0 | 32 |
| [Notas-940-Dark-00](Notas-940-Dark-00.png) | [JSON](Notas-940-Dark-00.json) | Aprovada no ensaio | 275,447,623,128 | 2 |
| [Notas-940-Dark-01](Notas-940-Dark-01.png) | [JSON](Notas-940-Dark-01.json) | Aprovada no ensaio | 275,355,623,220 | 7 |
| [Notas-940-Dark-18](Notas-940-Dark-18.png) | [JSON](Notas-940-Dark-18.json) | Rejeitada para 64 + folga | 263,224,0,0 | 22 |
| [Notas-940-Light-00](Notas-940-Light-00.png) | [JSON](Notas-940-Light-00.json) | Aprovada no ensaio | 275,447,623,128 | 2 |
| [Notas-940-Light-01](Notas-940-Light-01.png) | [JSON](Notas-940-Light-01.json) | Aprovada no ensaio | 275,355,623,220 | 7 |
| [Notas-940-Light-18](Notas-940-Light-18.png) | [JSON](Notas-940-Light-18.json) | Rejeitada para 64 + folga | 263,224,0,0 | 22 |

## Rolagem

Cada captura sucede ScrollToEnd real, retirada e nova medição. O corpo anterior não é mantido na região recalculada.

| Captura | Medições | Resultado após medir |
| --- | --- | --- |
| [Categorias-1200-Dark-18-rolado](Categorias-1200-Dark-18-rolado.png) | [JSON](Categorias-1200-Dark-18-rolado.json) | Região rejeitada |
| [Categorias-1200-Light-18-rolado](Categorias-1200-Light-18-rolado.png) | [JSON](Categorias-1200-Light-18-rolado.json) | Região rejeitada |
| [Categorias-940-Dark-18-rolado](Categorias-940-Dark-18-rolado.png) | [JSON](Categorias-940-Dark-18-rolado.json) | Região rejeitada |
| [Categorias-940-Light-18-rolado](Categorias-940-Light-18-rolado.png) | [JSON](Categorias-940-Light-18-rolado.json) | Região rejeitada |
| [Configuracoes-1200-Dark-01-rolado](Configuracoes-1200-Dark-01-rolado.png) | [JSON](Configuracoes-1200-Dark-01-rolado.json) | Região cabe; corpo retirado |
| [Configuracoes-1200-Dark-18-rolado](Configuracoes-1200-Dark-18-rolado.png) | [JSON](Configuracoes-1200-Dark-18-rolado.json) | Região cabe; corpo retirado |
| [Configuracoes-1200-Light-01-rolado](Configuracoes-1200-Light-01-rolado.png) | [JSON](Configuracoes-1200-Light-01-rolado.json) | Região cabe; corpo retirado |
| [Configuracoes-1200-Light-18-rolado](Configuracoes-1200-Light-18-rolado.png) | [JSON](Configuracoes-1200-Light-18-rolado.json) | Região cabe; corpo retirado |
| [Configuracoes-940-Dark-01-rolado](Configuracoes-940-Dark-01-rolado.png) | [JSON](Configuracoes-940-Dark-01-rolado.json) | Região rejeitada |
| [Configuracoes-940-Dark-18-rolado](Configuracoes-940-Dark-18-rolado.png) | [JSON](Configuracoes-940-Dark-18-rolado.json) | Região rejeitada |
| [Configuracoes-940-Light-01-rolado](Configuracoes-940-Light-01-rolado.png) | [JSON](Configuracoes-940-Light-01-rolado.json) | Região rejeitada |
| [Configuracoes-940-Light-18-rolado](Configuracoes-940-Light-18-rolado.png) | [JSON](Configuracoes-940-Light-18-rolado.json) | Região rejeitada |
| [Dashboard-1200-Dark-18-rolado](Dashboard-1200-Dark-18-rolado.png) | [JSON](Dashboard-1200-Dark-18-rolado.json) | Região rejeitada |
| [Dashboard-1200-Light-18-rolado](Dashboard-1200-Light-18-rolado.png) | [JSON](Dashboard-1200-Light-18-rolado.json) | Região rejeitada |
| [Dashboard-940-Dark-18-rolado](Dashboard-940-Dark-18-rolado.png) | [JSON](Dashboard-940-Dark-18-rolado.json) | Região rejeitada |
| [Dashboard-940-Light-18-rolado](Dashboard-940-Light-18-rolado.png) | [JSON](Dashboard-940-Light-18-rolado.json) | Região rejeitada |
| [Notas-1200-Dark-18-rolado](Notas-1200-Dark-18-rolado.png) | [JSON](Notas-1200-Dark-18-rolado.json) | Região rejeitada |
| [Notas-1200-Light-18-rolado](Notas-1200-Light-18-rolado.png) | [JSON](Notas-1200-Light-18-rolado.json) | Região rejeitada |
| [Notas-940-Dark-18-rolado](Notas-940-Dark-18-rolado.png) | [JSON](Notas-940-Dark-18-rolado.json) | Região rejeitada |
| [Notas-940-Light-18-rolado](Notas-940-Light-18-rolado.png) | [JSON](Notas-940-Light-18-rolado.json) | Região rejeitada |

## Interrupcoes e input

Cada grupo vai de `00` a `04`: corpo ativo → modal/editor → mensagem → lista acrescida de 18 registros → navegação para Início. É uma sequência por eventos, sem pretensão de medir sua duração real. Estado modal e navegação têm `MapaAtivo=false`; os retângulos da última medição não autorizam ocupação nesse estado.

| Grupo | Controle positivo e contadores |
| --- | --- |
| Notas-1200-Light | [Input](interrupcoes-Notas-1200-Light-input.json) |
| Notas-940-Dark | [Input](interrupcoes-Notas-940-Dark-input.json) |
| Categorias-1200-Dark | [Input](interrupcoes-Categorias-1200-Dark-input.json) |
| Categorias-940-Light | [Input](interrupcoes-Categorias-940-Light-input.json) |

Os contadores ficam em zero durante o percurso; o teste calibra um comando sintético uma vez e o total continua em um após navegação. Não há clique real do Windows nem execução de operação do domínio.

| Captura | Medições | Mapa |
| --- | --- | --- |
| [interrupcoes-Categorias-1200-Dark-00-ativo](interrupcoes-Categorias-1200-Dark-00-ativo.png) | [JSON](interrupcoes-Categorias-1200-Dark-00-ativo.json) | Medido, cabe |
| [interrupcoes-Categorias-1200-Dark-01-modal-retirado](interrupcoes-Categorias-1200-Dark-01-modal-retirado.png) | [JSON](interrupcoes-Categorias-1200-Dark-01-modal-retirado.json) | Invalidado; corpo retirado |
| [interrupcoes-Categorias-1200-Dark-02-mensagem](interrupcoes-Categorias-1200-Dark-02-mensagem.png) | [JSON](interrupcoes-Categorias-1200-Dark-02-mensagem.json) | Medido, cabe |
| [interrupcoes-Categorias-1200-Dark-03-lista-cresceu](interrupcoes-Categorias-1200-Dark-03-lista-cresceu.png) | [JSON](interrupcoes-Categorias-1200-Dark-03-lista-cresceu.json) | Medido, rejeitado |
| [interrupcoes-Categorias-1200-Dark-04-navegacao](interrupcoes-Categorias-1200-Dark-04-navegacao.png) | [JSON](interrupcoes-Categorias-1200-Dark-04-navegacao.json) | Invalidado; corpo retirado |
| [interrupcoes-Categorias-940-Light-00-ativo](interrupcoes-Categorias-940-Light-00-ativo.png) | [JSON](interrupcoes-Categorias-940-Light-00-ativo.json) | Medido, cabe |
| [interrupcoes-Categorias-940-Light-01-modal-retirado](interrupcoes-Categorias-940-Light-01-modal-retirado.png) | [JSON](interrupcoes-Categorias-940-Light-01-modal-retirado.json) | Invalidado; corpo retirado |
| [interrupcoes-Categorias-940-Light-02-mensagem](interrupcoes-Categorias-940-Light-02-mensagem.png) | [JSON](interrupcoes-Categorias-940-Light-02-mensagem.json) | Medido, cabe |
| [interrupcoes-Categorias-940-Light-03-lista-cresceu](interrupcoes-Categorias-940-Light-03-lista-cresceu.png) | [JSON](interrupcoes-Categorias-940-Light-03-lista-cresceu.json) | Medido, rejeitado |
| [interrupcoes-Categorias-940-Light-04-navegacao](interrupcoes-Categorias-940-Light-04-navegacao.png) | [JSON](interrupcoes-Categorias-940-Light-04-navegacao.json) | Invalidado; corpo retirado |
| [interrupcoes-Notas-1200-Light-00-ativo](interrupcoes-Notas-1200-Light-00-ativo.png) | [JSON](interrupcoes-Notas-1200-Light-00-ativo.json) | Medido, cabe |
| [interrupcoes-Notas-1200-Light-01-modal-retirado](interrupcoes-Notas-1200-Light-01-modal-retirado.png) | [JSON](interrupcoes-Notas-1200-Light-01-modal-retirado.json) | Invalidado; corpo retirado |
| [interrupcoes-Notas-1200-Light-02-mensagem](interrupcoes-Notas-1200-Light-02-mensagem.png) | [JSON](interrupcoes-Notas-1200-Light-02-mensagem.json) | Medido, cabe |
| [interrupcoes-Notas-1200-Light-03-lista-cresceu](interrupcoes-Notas-1200-Light-03-lista-cresceu.png) | [JSON](interrupcoes-Notas-1200-Light-03-lista-cresceu.json) | Medido, rejeitado |
| [interrupcoes-Notas-1200-Light-04-navegacao](interrupcoes-Notas-1200-Light-04-navegacao.png) | [JSON](interrupcoes-Notas-1200-Light-04-navegacao.json) | Invalidado; corpo retirado |
| [interrupcoes-Notas-940-Dark-00-ativo](interrupcoes-Notas-940-Dark-00-ativo.png) | [JSON](interrupcoes-Notas-940-Dark-00-ativo.json) | Medido, cabe |
| [interrupcoes-Notas-940-Dark-01-modal-retirado](interrupcoes-Notas-940-Dark-01-modal-retirado.png) | [JSON](interrupcoes-Notas-940-Dark-01-modal-retirado.json) | Invalidado; corpo retirado |
| [interrupcoes-Notas-940-Dark-02-mensagem](interrupcoes-Notas-940-Dark-02-mensagem.png) | [JSON](interrupcoes-Notas-940-Dark-02-mensagem.json) | Medido, cabe |
| [interrupcoes-Notas-940-Dark-03-lista-cresceu](interrupcoes-Notas-940-Dark-03-lista-cresceu.png) | [JSON](interrupcoes-Notas-940-Dark-03-lista-cresceu.json) | Medido, rejeitado |
| [interrupcoes-Notas-940-Dark-04-navegacao](interrupcoes-Notas-940-Dark-04-navegacao.png) | [JSON](interrupcoes-Notas-940-Dark-04-navegacao.json) | Invalidado; corpo retirado |

## Retirada, busca e resize

Notas/escuro: tamanho normal → mínimo → proximidade sintética → digitação sintética → restaurado. Pontos distantes permitem continuar; proximidade retira. A cena não recomeça automaticamente. A troca de tamanho é no layout da raiz, sem maximização do Windows.

| Captura | Dados |
| --- | --- |
| [retirada-00-ativo-1200](retirada-00-ativo-1200.png) | [JSON](retirada-00-ativo-1200.json) |
| [retirada-01-minimo](retirada-01-minimo.png) | [JSON](retirada-01-minimo.json) |
| [retirada-02-proximidade](retirada-02-proximidade.png) | [JSON](retirada-02-proximidade.json) |
| [retirada-03-digitacao](retirada-03-digitacao.png) | [JSON](retirada-03-digitacao.json) |
| [retirada-04-restaurado](retirada-04-restaurado.png) | [JSON](retirada-04-restaurado.json) |

## Integridade e limites

Os 96 PNGs foram decodificados para conferir integridade e dimensões. A inspeção visual direta abrangeu amostras de Configurações, Categorias, Notas, Início, sequência R01 e modal; isso não equivale a revisão humana individual dos 96 arquivos. Ver pendências de DPI, janela visível, entrada lateral completa e sprites no relatório principal.

