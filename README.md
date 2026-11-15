# baseline
NASA Space Apps Challenge 2026 project

BASELINE helps astronauts track their vital signs on a long mission and flags readings that look
off. All crew data in the app is **synthetic**. It gives **decision support, not diagnosis**.

## Run it with Docker (easiest)

You only need [Docker](https://docs.docker.com/get-docker/). You don't need .NET or Node.

**1. Build the image** (run this from the project folder; the first build takes a few minutes):

```sh
docker build -t baseline .
```

**2. Start it:**

```sh
docker run -p 3000:3000 -p 5074:5074 baseline
```

**3. Open the app:** go to **http://localhost:3000** in your browser.

That's it. Press `Ctrl+C` in the terminal to stop it.

### What runs inside the container

| Part     | Address               | What it is                                         |
| -------- | --------------------- | -------------------------------------------------- |
| Frontend | http://localhost:3000 | The app you click through (crew and ground screens) |
| Backend  | http://localhost:5074 | The API that the frontend talks to                  |

Both run in one container, and the health limits come from the files in [config/](config/).

### Handy extras

**Keep the demo data between runs.** By default the data resets each time you create a new
container. To keep it, add a volume:

```sh
docker run -p 3000:3000 -p 5074:5074 -v baseline-data:/app/data baseline
```

**Reset the demo** (reload the synthetic mission from day 0):

```sh
docker run -p 3000:3000 -p 5074:5074 -v baseline-data:/app/data baseline reseed
```

**Run the tests only** (backend tests, plus the frontend type check and lint):

```sh
docker build --target test .
```

**Run it on a server, not your own computer.** The browser has to be able to reach the backend,
so give the backend's public address at build time, and allow the frontend's address:

```sh
docker build -t baseline --build-arg API_URL=http://my-server:5074 .
docker run -p 3000:3000 -p 5074:5074 \
  -e Cors__AllowedOrigins__0=http://my-server:3000 baseline
```

### Troubleshooting

- **"port is already allocated"**: something else is using port 3000 or 5074. Stop it, or change
  only the left-hand port in `-p`. If you change 3000, also set
  `-e Cors__AllowedOrigins__0=http://localhost:<new port>`.
- **The app opens but shows no data**: check that the backend answers at
  http://localhost:5074/api/config/metrics.

## Automatic builds (GitHub Actions)

Each push to `main`, and each pull request, runs
[.github/workflows/build.yml](.github/workflows/build.yml). It:

1. runs the backend tests and the frontend checks,
2. builds the Docker image,
3. starts the container and checks that both the frontend and the backend answer.

You can see the results in the repository's **Actions** tab. You can also run it by hand from there
(**Build** → **Run workflow**).

## Run without Docker (for development)

See [backend/README.md](backend/README.md) and [frontend/README.md](frontend/README.md). In short:

```sh
cd backend && dotnet run          # API at http://localhost:5074
cd frontend && npm install && npm run dev   # app at http://localhost:5173
```
