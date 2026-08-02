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
    id: "silverstone_afternoon_tea",
    environment: "call_Kz4AcGfmVC3Rt4mTXgrAEB6g.png",
    output: "track_layout_uk.png",
  },
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

function offsetPoints(points, offset) {
  return points.map((point, index) => {
    const previous = points[(index - 1 + points.length) % points.length];
    const next = points[(index + 1) % points.length];
    const tangentX = next[0] - previous[0];
    const tangentY = next[1] - previous[1];
    const length = Math.hypot(tangentX, tangentY) || 1;
    const normalX = -tangentY / length;
    const normalY = tangentX / length;
    return [point[0] + normalX * offset, point[1] + normalY * offset];
  });
}

function pathFromPoints(points) {
  return points
    .map(
      (point, index) =>
        `${index === 0 ? "M" : "L"}${point[0].toFixed(2)} ${point[1].toFixed(2)}`,
    )
    .join(" ") + " Z";
}

function createTrackOverlay(config, trackId) {
  const points = config.cells.map((cell) => [
    cell.position.x * width,
    (1 - cell.position.y) * height,
  ]);
  const laneCount = trackId === "indianapolis_burger" ? 4 : 2;
  const laneSpacing = 36;
  const laneWidth = 48;
  const laneOffsets = Array.from(
    { length: laneCount },
    (_, index) => (index - (laneCount - 1) * 0.5) * laneSpacing,
  );
  const pathData = pathFromPoints(points);

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

  const lanePaths = laneOffsets
    .map((offset) => {
      const lanePath = pathFromPoints(offsetPoints(points, offset));
      return `
      <path d="${lanePath}" fill="none" stroke="#30343b" stroke-width="${laneWidth}"
            stroke-linejoin="round" stroke-linecap="round"/>`;
    })
    .join("\n");
  const separatorPaths = laneOffsets
    .slice(0, -1)
    .map((offset, index) => {
      const separatorPath = pathFromPoints(
        offsetPoints(points, (offset + laneOffsets[index + 1]) * 0.5),
      );
      return `
      <path d="${separatorPath}" fill="none" stroke="#d8d2c5" stroke-width="4"
            stroke-dasharray="18 16" stroke-linejoin="round"/>`;
    })
    .join("\n");

  const roadWidth = laneWidth + (laneCount - 1) * laneSpacing;
  return Buffer.from(`
    <svg width="${width}" height="${height}" xmlns="http://www.w3.org/2000/svg">
      <path d="${pathData}" fill="none" stroke="#f4f0e7" stroke-width="${roadWidth + 24}"
            stroke-linejoin="round" stroke-linecap="round"/>
      <path d="${pathData}" fill="none" stroke="#d94b43" stroke-width="${roadWidth + 8}"
            stroke-dasharray="46 46" stroke-linejoin="round" stroke-linecap="butt"/>
      ${lanePaths}
      ${separatorPaths}
      <line x1="${lineStartX}" y1="${lineStartY}" x2="${lineEndX}" y2="${lineEndY}"
            stroke="#ffffff" stroke-width="18"/>
      <line x1="${lineStartX}" y1="${lineStartY}" x2="${lineEndX}" y2="${lineEndY}"
            stroke="#17191d" stroke-width="18" stroke-dasharray="14 14"/>
    </svg>`);
}

const requestedTrackIds = new Set(process.argv.slice(3));

async function main() {
  for (const track of tracks) {
    if (requestedTrackIds.size > 0 && !requestedTrackIds.has(track.id)) {
      continue;
    }
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
      .composite([{ input: createTrackOverlay(config, track.id), blend: "over" }])
      .png({ compressionLevel: 9, adaptiveFiltering: true })
      .toFile(outputPath);
    console.log(`${track.id} -> ${track.output}`);
  }
}

main().catch((error) => {
  console.error(error);
  process.exit(1);
});
