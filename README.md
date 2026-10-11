# Buteco Digital

Buteco de esquina low-poly (Unity 6, URP) em modo **sandbox**: você chega pela calçada em primeira pessoa e
escolhe o que fazer — pedir uma gelada ou uma coxinha pro Seu Tião no balcão, sentar na roda de pagode (ou no
balcão, ou nas mesas da calçada) e ouvir o samba, ou chamar a galera pra mesa de sinuca: bola 8, lisas x
listradas, 1x1 e 2x2, controle compartilhado ou um controle por jogador.

## Rodar no editor
Abra `Assets/_Buteco/Scenes/Buteco.unity` e dê Play.

## Compilar e rodar nativo no Linux
Precisa do **Unity 6000.6.5f1** (o mesmo de `ProjectSettings/ProjectVersion.txt`) com o módulo
**Linux Build Support (Mono)** — no Unity Hub: *Installs → ⚙ → Add modules*. O build usa Mono, não precisa de IL2CPP.

**Pelo editor:** menu **Buteco → Compilar para Linux**. Sai em `Builds/Linux/` (a pasta já está no `.gitignore`).
O mesmo dá pra fazer à mão em *File → Build Profiles → Linux* com a cena `Buteco.unity`.

**Pela linha de comando** (com o editor fechado — o Unity não abre o mesmo projeto duas vezes):
```bash
~/Unity/Hub/Editor/6000.6.5f1/Editor/Unity -batchmode -quit -nographics \
  -projectPath ~/projetos/games/buteco-digital \
  -executeMethod Buteco.EditorTools.ButecoBuild.Linux \
  -logFile build.log
# opcionais: -buildPath <pasta>  (padrão Builds/Linux)   -dev  (build de desenvolvimento, com profiler/console)
```
Sai com código 0 se deu certo; o resumo fica no log (`[Buteco] Build Linux: Succeeded …`).

**Rodar:**
```bash
cd Builds/Linux
./ButecoDigital.x86_64
# janela em vez de tela cheia (ou troque em Opções → Vídeo):
./ButecoDigital.x86_64 -screen-fullscreen 0 -screen-width 1600 -screen-height 900
```
- Pra levar pra outra máquina, copie a pasta `Builds/Linux` inteira **menos** `ButecoDigital_BackUpThisFolder_ButDontShipItWithYourGame/`.
- Log do jogo: `~/.config/unity3d/Buteco Digital/Buteco Digital/Player.log`. As opções ficam em `prefs`, na mesma pasta
  (apague o arquivo pra voltar tudo ao padrão).
- Se der problema de vídeo, troque a API gráfica na linha de comando: `-force-glcore` (OpenGL) ou `-force-vulkan`.
- Controle: Xbox/PlayStation/genéricos via SDL. Se o controle não aparecer, confira se seu usuário lê `/dev/input/event*`
  (grupo `input`) ou se há regra udev do fabricante (ex.: pacote `steam-devices`).

## Opções
No título, nas pausas e no menu da sinuca tem **Opções**, em três abas (salvas entre sessões):
- **Geral** — sensibilidade do mouse e do analógico, inverter olhar, campo de visão, balanço da cabeça, dicas de controle na tela.
- **Vídeo** — resolução, modo de tela (tela cheia / exclusiva / janela), VSync, limite de FPS, escala de renderização,
  antisserrilhado (MSAA), sombras e pós-processamento.
- **Música** — volume geral, música da roda, efeitos e ambiente da viela; dá pra pedir a próxima música pra roda.

Na tela de opções: setas/analógico escolhem e ajustam, **LB/RB** ou **Q/E** trocam de aba, **Esc/B** volta.
Resolução e modo de tela só valem no jogo compilado (no editor quem manda é a janela Game).

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
| Mirar (sem linha de mira — é no olho) | Mouse / A-D (Shift = fino) | Analógico esq. (dir. = fino) |
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
- `Runtime/UI/ButecoUI.cs` — interface montada por código (inclui o menu de Opções; `OptionStepper` = seletor ‹ ›).
- `Runtime/GameSettings.cs` / `SoundVolume.cs` — opções salvas em PlayerPrefs; volume por categoria (música, efeitos,
  ambiente) sem AudioMixer; no editor o asset do URP e o VSync originais voltam ao sair do Play.
- `Editor/ButecoBuild.cs` — build Linux (menu e `-executeMethod`).
- `Editor/ButecoBuilder.cs` — monta o bar, a viela e a comunidade; `ButecoBuilder.Sandbox.cs` — colisões, gente,
  instrumentos, cadeiras sentáveis e o jogador.
