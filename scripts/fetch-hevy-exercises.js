/**
 * Script to fetch all exercise templates from Hevy API
 * Run with: node scripts/fetch-hevy-exercises.js
 */

const HEVY_API_KEY = 'bbc441b6-532e-4574-91c4-41b227f9f044';
const HEVY_API_BASE_URL = 'https://api.hevyapp.com/v1';

async function fetchAllExerciseTemplates() {
  const allTemplates = [];
  let page = 1;
  let hasMore = true;

  console.log('Fetching exercise templates from Hevy API...\n');

  while (hasMore) {
    const url = `${HEVY_API_BASE_URL}/exercise_templates?page=${page}&pageSize=100`;
    console.log(`Fetching page ${page}...`);

    const response = await fetch(url, {
      headers: {
        'api-key': HEVY_API_KEY,
        'Content-Type': 'application/json',
      },
    });

    if (!response.ok) {
      throw new Error(`API error: ${response.status} ${response.statusText}`);
    }

    const data = await response.json();
    allTemplates.push(...data.exercise_templates);

    console.log(`  Got ${data.exercise_templates.length} exercises (page ${page} of ${data.page_count})`);

    hasMore = page < data.page_count;
    page++;
  }

  console.log(`\nTotal exercises fetched: ${allTemplates.length}\n`);
  return allTemplates;
}

// Equipment mapping from Hevy to A2S
const equipmentMapping = {
  'barbell': 'Barbell',
  'dumbbell': 'Dumbbell',
  'machine': 'Machine',
  'cable': 'Cable',
  'none': 'Bodyweight',
  'bodyweight': 'Bodyweight',
  'kettlebell': 'Dumbbell', // Map to closest
  'plate': 'Barbell',
  'resistance_band': 'Cable',
  'suspension': 'Bodyweight',
  'other': 'Machine',
};

function generateCSharpCode(exercises) {
  const lines = exercises.map(ex => {
    const equipment = equipmentMapping[ex.equipment_category] || 'Machine';
    const description = ex.title.replace(/"/g, '\\"');

    return `        new ExerciseTemplate(
            Name: "${ex.title}",
            Equipment: EquipmentType.${equipment},
            DefaultRepRange: RepRange.Common.Medium,
            DefaultSets: 3,
            Description: "Hevy exercise - ${ex.muscle_group}"),`;
  });

  return lines.join('\n\n');
}

function generateTypeScriptMapping(exercises) {
  const mapping = {};
  exercises.forEach(ex => {
    // Normalize exercise name for matching
    const normalizedName = ex.title.toLowerCase().trim();
    mapping[normalizedName] = {
      id: ex.id,
      title: ex.title,
      muscle_group: ex.muscle_group,
      equipment_category: ex.equipment_category,
      is_custom: ex.is_custom,
    };
  });

  return JSON.stringify(mapping, null, 2);
}

async function main() {
  try {
    const exercises = await fetchAllExerciseTemplates();

    // Group by muscle group for readability
    const byMuscle = {};
    exercises.forEach(ex => {
      const muscle = ex.muscle_group || 'other';
      if (!byMuscle[muscle]) byMuscle[muscle] = [];
      byMuscle[muscle].push(ex);
    });

    console.log('Exercises by muscle group:');
    Object.entries(byMuscle).forEach(([muscle, exs]) => {
      console.log(`  ${muscle}: ${exs.length}`);
    });

    // Output the exercises as JSON for the mapping service
    const fs = require('fs');

    // Save full exercise data
    fs.writeFileSync(
      'scripts/hevy-exercises.json',
      JSON.stringify(exercises, null, 2)
    );
    console.log('\nSaved full exercise data to scripts/hevy-exercises.json');

    // Save the mapping
    fs.writeFileSync(
      'scripts/hevy-exercise-mapping.json',
      generateTypeScriptMapping(exercises)
    );
    console.log('Saved exercise mapping to scripts/hevy-exercise-mapping.json');

    // Generate C# code for new exercises
    // Only include custom exercises or exercises not in our library
    const customExercises = exercises.filter(ex => ex.is_custom);
    if (customExercises.length > 0) {
      console.log(`\nFound ${customExercises.length} custom exercises:`);
      customExercises.forEach(ex => {
        console.log(`  - ${ex.title} (${ex.muscle_group}, ${ex.equipment_category})`);
      });
    }

    console.log('\n=== Summary ===');
    console.log(`Total Hevy exercises: ${exercises.length}`);
    console.log(`Custom exercises: ${customExercises.length}`);
    console.log(`Standard exercises: ${exercises.length - customExercises.length}`);

  } catch (error) {
    console.error('Error:', error.message);
    process.exit(1);
  }
}

main();
