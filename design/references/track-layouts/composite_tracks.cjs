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
    environment: "design/references/track-layouts/environments/nurburgring_bier_environment.png",
    projectEnvironment: true,
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

// Keep the artwork generator in lockstep with TrackDataLoader.ConfigToWorldPositions.
// Authored landmarks (corners, start/finish and pit markers) remain fixed while
// the non-landmark points in each run are distributed by arc length. This makes
// the painted road centerline and the runtime node path share the same geometry.
function resampleAnchoredPath(source, cells) {
  if (!Array.isArray(source) || source.length <= 2 || !Array.isArray(cells) || cells.length !== source.length) {
    return source.map((point) => [...point]);
  }

  const result = source.map((point) => [...point]);
  const anchors = cells.map((cell) =>
    cell.type === "corner" ||
    cell.type === "start_finish" ||
    cell.type === "pit_entry" ||
    cell.type === "pit_exit",
  );
  const anchorIndices = anchors
    .map((isAnchor, index) => (isAnchor ? index : -1))
    .filter((index) => index >= 0);

  if (anchorIndices.length === 0) {
    return resampleClosedPath(source);
  }

  for (let anchorOrdinal = 0; anchorOrdinal < anchorIndices.length; anchorOrdinal += 1) {
    const start = anchorIndices[anchorOrdinal];
    const end = anchorIndices[(anchorOrdinal + 1) % anchorIndices.length];
    const interiorCount = (end - start - 1 + source.length) % source.length;
    if (interiorCount <= 0) {
      continue;
    }

    const run = Array.from({ length: interiorCount + 2 }, (_, index) =>
      source[(start + index) % source.length],
    );
    const lengths = [];
    const cumulative = [0];
    let total = 0;
    for (let index = 0; index < run.length - 1; index += 1) {
      const dx = run[index + 1][0] - run[index][0];
      const dy = run[index + 1][1] - run[index][1];
      const length = Math.hypot(dx, dy);
      lengths.push(length);
      total += length;
      cumulative.push(total);
    }

    if (total <= Number.EPSILON) {
      continue;
    }

    for (let interior = 1; interior <= interiorCount; interior += 1) {
      const target = (total * interior) / (interiorCount + 1);
      let segment = 0;
      while (segment < lengths.length - 1 && cumulative[segment + 1] < target) {
        segment += 1;
      }

      const segmentLength = lengths[segment];
      const t = segmentLength > Number.EPSILON
        ? (target - cumulative[segment]) / segmentLength
        : 0;
      const from = run[segment];
      const to = run[segment + 1];
      result[(start + interior) % source.length] = [
        from[0] + (to[0] - from[0]) * Math.max(0, Math.min(1, t)),
        from[1] + (to[1] - from[1]) * Math.max(0, Math.min(1, t)),
      ];
    }
  }

  return result;
}

function resampleClosedPath(source) {
  if (!Array.isArray(source) || source.length <= 2) {
    return source.map((point) => [...point]);
  }

  const count = source.length;
  const segmentLengths = [];
  const cumulative = [0];
  let total = 0;
  for (let index = 0; index < count; index += 1) {
    const from = source[index];
    const to = source[(index + 1) % count];
    const length = Math.hypot(to[0] - from[0], to[1] - from[1]);
    segmentLengths.push(length);
    total += length;
    cumulative.push(total);
  }

  if (total <= Number.EPSILON) {
    return source.map((point) => [...point]);
  }

  const result = [];
  const spacing = total / count;
  let segmentIndex = 0;
  for (let index = 0; index < count; index += 1) {
    const target = spacing * index;
    while (segmentIndex < count - 1 && cumulative[segmentIndex + 1] < target) {
      segmentIndex += 1;
    }

    const segmentLength = segmentLengths[segmentIndex];
    const t = segmentLength > Number.EPSILON
      ? (target - cumulative[segmentIndex]) / segmentLength
      : 0;
    const from = source[segmentIndex];
    const to = source[(segmentIndex + 1) % count];
    const clamped = Math.max(0, Math.min(1, t));
    result.push([
      from[0] + (to[0] - from[0]) * clamped,
      from[1] + (to[1] - from[1]) * clamped,
    ]);
  }
  return result;
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
  // Sample in the same 16:9 metric used by the runtime world (30 x 16.875).
  // Sampling in raw 0-1 coordinates would weight x/y equally and diverge on
  // long straights once the layout is rendered into the non-square world.
  const authoredPoints = config.cells.map((cell) => [
    cell.position.x * width,
    cell.position.y * height,
  ]);
  const sampledPoints = resampleAnchoredPath(authoredPoints, config.cells);
  const points = sampledPoints.map((point) => [
    point[0],
    height - point[1],
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
    const environmentPath = track.projectEnvironment
      ? path.join(projectRoot, track.environment)
      : path.join(generatedRoot, track.environment);
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
