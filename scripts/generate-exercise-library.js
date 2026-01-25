const fs = require('fs');
const exercises = JSON.parse(fs.readFileSync('scripts/hevy-exercises.json', 'utf8'));

// Map Hevy equipment to A2S EquipmentType
const equipmentMap = {
  'barbell': 'Barbell',
  'dumbbell': 'Dumbbell',
  'machine': 'Machine',
  'cable': 'Cable',
  'bodyweight': 'Bodyweight',
  'none': 'Bodyweight',
  'kettlebell': 'Dumbbell',
  'plate': 'Barbell',
  'resistance_band': 'Cable',
  'suspension': 'Bodyweight',
  'other': 'Machine',
  'ez_bar': 'Barbell',
  'trap_bar': 'Barbell',
  'smith_machine': 'Machine',
};

// Generate C# code for exercises
const csharpEntries = exercises.map(ex => {
  const equipment = equipmentMap[ex.equipment] || 'Machine';
  const name = ex.title.replace(/"/g, '\\"');
  const muscle = ex.primary_muscle_group || 'other';

  return `        new ExerciseTemplate(
            Name: "${name}",
            Equipment: EquipmentType.${equipment},
            DefaultRepRange: RepRange.Common.Medium,
            DefaultSets: 3,
            Description: "Hevy: ${muscle}")`;
}).join(',\n\n');

fs.writeFileSync('scripts/hevy-exercises-csharp.txt', csharpEntries);
console.log(`Generated C# code for ${exercises.length} exercises`);
