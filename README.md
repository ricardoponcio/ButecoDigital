# Buteco Digital

Buteco de esquina low-poly (Unity 6, URP) em modo **sandbox**: você chega pela calçada em primeira pessoa e
escolhe o que fazer — pedir uma gelada ou uma coxinha pro Seu Tião no balcão, sentar na roda de pagode (ou no
balcão, ou nas mesas da calçada) e ouvir o samba, ou chamar a galera pra mesa de sinuca: bola 8, lisas x
listradas, 1x1 e 2x2, controle compartilhado ou um controle por jogador.

## Rodar
Abra `Assets/_Buteco/Scenes/Buteco.unity` e dê Play.

## Regerar
- Cena inteira (procedural): menu **Buteco → Construir Cena** (recria do zero; não edite a cena à mão, edite `ButecoBuilder.cs`).
- Texturas: `python3 Tools/gen_textures.py` · Músicas da roda/efeitos: `python3 Tools/gen_audio.py` (tudo sintetizado, sem samples de terceiros).

## Controles — passeio pelo buteco
| | Teclado + mouse | Controle |
|---|---|---|
| Andar / correr | WASD / Shift | Analógico esq. / L3 (ou LT) |
| Olhar | Mouse | Analógico dir. |
| Interagir (sentar, pedir, jogar sinuca, pagar) | E | A |
| Gole / encher o copo / mordida / palma | Clique ou Espaço | RT ou X |
| Levantar | Q ou botão dir. | B |
| Pausa | Esc | Start |

## Controles — sinuca
| | Teclado + mouse | Controle |
|---|---|---|
| Mirar | Mouse / A-D (Shift = fino) | Analógico esq. (dir. = fino) |
| Tacada | Segure clique/Espaço e solte | Segure A ou aperte RT e solte |
| Cancelar tacada | Botão direito / Backspace | B |
| Vista de cima | Tab | Y |
| Zoom | Roda / Q-E | LB / RB |
| Bola na mão | Mouse ou WASD + clique/Espaço | Analógico + A |
| Pausa | Esc | Start |

## Código (`Assets/_Buteco/Scripts`)
- `Runtime/Sandbox/ButecoSandbox.cs` — estados do jogo: título → passeio → mesa de sinuca → volta pro bar.
- `Runtime/Sandbox/ButecoPlayer.cs` / `PlayerHands.cs` — freguês em primeira pessoa, conta, gelada/copo/coxinha na mão.
- `Runtime/Sandbox/Interactable.cs` + `Seat`, `CounterService`, `PoolSpot` — tudo que dá pra usar olhando + E.
- `Runtime/Sandbox/RodaDePagode.cs`, `Musician.cs`, `Patron.cs`, `Bartender.cs`, `NpcRig.cs` — a roda (repertório,
  aplauso, batida sincronizada com a animação), fregueses e o Seu Tião.
- `Runtime/Pool/PoolPhysics.cs` — física 2D própria e determinística (sem PhysX), pronta para rede.
- `Runtime/Pool/PoolRules.cs` — regras da bola 8 de buteco (sem cantar caçapa, falta = bola na mão).
- `Runtime/Pool/PoolGame.cs` — fases, turnos, tacada; `TryShoot`/`PlaceCueBall`/`FastForward` servem para IA, rede e testes.
- `Runtime/Players/` — partida (times, ordem A1-B1-A2-B2) e entrada multi-dispositivo.
- `Runtime/UI/ButecoUI.cs` — interface montada por código.
- `Editor/ButecoBuilder.cs` — monta o bar, a viela e a comunidade; `ButecoBuilder.Sandbox.cs` — colisões, gente,
  instrumentos, cadeiras sentáveis e o jogador.
