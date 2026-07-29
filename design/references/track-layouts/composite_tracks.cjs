const fs = require("fs");
const path = require("path");
const sharp = require(process.argv[2]);

const projectRoot = process.cwd();
const generatedRoot =
  "C:/Users/chamber/.codex/generated_images/019fae84-50d4-7e82-ba52-8736500d5760";
const outputDirectory = path.join(projectRoot, "Assets/Sprites/Track");
const width = 3840;
const height = 2160;

const tracks = [
  {
    id: "suzuka_sushi",
    environment: "call_8uHcpthPK1ri5cfucourCUYs.png",
    output: "track_layout_jp.png",
  },
  {
    id: "shanghai_dim_sum",
    environment: "call_0Uk71kUt0OL5Ngi7hzNHtzHA.png",
    output: "track_layout_cn.png",
  },
  {
    id: "nurburgring_bier",
    environment: "call_DIBbQgBPe9otdJgNVrZT7KVi.png",
    output: "track_layout_de.png",
  },
  {
    id: "monza_pasta",
    environment: "call_Dcsk6uUTaUOLDAQ2OXBWWxsC.png",
    output: "track_layout_it.png",
  },
  {
    id: "indianapolis_burger",
    environment: "call_ipB9Y2cnjFWC3Ar4jaRqjrrK.png",
    output: "track_layout_us.png",
  },
  {
    id: "nurburgring_24h_endurance",
    environment: "call_sghnwKylQo24NhKCt3lmaviF.png",
    output: "track_layout_de_endurance.png",
  },
  {
    id: "le_mans_old_mulsanne",
    environment: "call_gy2Pdrc9uBCM0SfE6MVwB6bL.png",
    output: "track_layout_fr_lemans.png",
  },
];

function createTrackOverlay(config) {
  const points = config.cells.map((cell) => [
    cell.position.x * width,
    (1 - cell.position.y) * height,
  ]);
  const pathData =
    points
      .map(
        (point, index) =>
          `${index === 0 ? "M" : "L"}${point[0].toFixed(2)} ${point[1].toFixed(2)}`,
      )
      .join(" ") + " Z";

  const start = points[0];
  const previous = points[points.length - 1];
  const next = points[1];
  const tangentX = next[0] - previous[0];
  const tangentY = next[1] - previous[1];
  const length = Math.hypot(tangentX, tangentY) || 1;
  const normalX = -tangentY / length;
  const normalY = tangentX / length;
  const startHalfWidth = 58;
  const lineStartX = start[0] - normalX * startHalfWidth;
  const lineStartY = start[1] - normalY * startHalfWidth;
  const lineEndX = start[0] + normalX * startHalfWidth;
  const lineEndY = start[1] + normalY * startHalfWidth;

  return Buffer.from(`
    <svg width="${width}" height="${height}" xmlns="http://www.w3.org/2000/svg">
      <path d="${pathData}" fill="none" stroke="#f4f0e7" stroke-width="154"
            stroke-linejoin="round" stroke-linecap="round"/>
      <path d="${pathData}" fill="none" stroke="#d94b43" stroke-width="154"
            stroke-dasharray="46 46" stroke-linejoin="round" stroke-linecap="butt"/>
      <path d="${pathData}" fill="none" stroke="#30343b" stroke-width="116"
            stroke-linejoin="round" stroke-linecap="round"/>
      <path d="${pathData}" fill="none" stroke="#4c5159" stroke-width="4"
            stroke-linejoin="round"/>
      <line x1="${lineStartX}" y1="${lineStartY}" x2="${lineEndX}" y2="${lineEndY}"
            stroke="#ffffff" stroke-width="18"/>
      <line x1="${lineStartX}" y1="${lineStartY}" x2="${lineEndX}" y2="${lineEndY}"
            stroke="#17191d" stroke-width="18" stroke-dasharray="14 14"/>
    </svg>`);
}

async function main() {
  for (const track of tracks) {
    const configPath = path.join(
      projectRoot,
      "Assets/Resources/Configs/Tracks",
      `${track.id}.json`,
    );
    const config = JSON.parse(fs.readFileSync(configPath, "utf8"));
    const environmentPath = path.join(generatedRoot, track.environment);
    const outputPath = path.join(outputDirectory, track.output);

    await sharp(environmentPath)
      .resize(width, height, { fit: "cover", position: "centre" })
      .composite([{ input: createTrackOverlay(config), blend: "over" }])
      .png({ compressionLevel: 9, adaptiveFiltering: true })
      .toFile(outputPath);
    console.log(`${track.id} -> ${track.output}`);
  }
}

main().catch((error) => {
  console.error(error);
  process.exit(1);
});
