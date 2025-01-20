import { Condition } from '../../types/dice';

interface Props {
  conditions: Condition[];
  onUpdate: (conditions: Condition[]) => void;
}

const conditionTypes = [
  { value: 'sumEquals', text: 'Sum equals' },
  { value: 'sumGreaterThan', text: 'Sum greater than' },
  { value: 'sumLessThan', text: 'Sum less than' },
  { value: 'atLeastOne', text: 'At least one die shows' },
  { value: 'all', text: 'All dice show' },
  { value: 'countMatching', text: 'Exactly N dice show' }
] as const;

export function Conditions({ conditions, onUpdate }: Props) {
  const handleAdd = () => {
    onUpdate([...conditions, { type: 'sumGreaterThan', value: '10' }]);
  };

  const handleRemove = (index: number) => {
    onUpdate(conditions.filter((_, i) => i !== index));
  };

  const handleChange = (index: number, field: keyof Condition, value: string) => {
    const updatedConditions = [...conditions];
    updatedConditions[index] = {
      ...updatedConditions[index],
      [field]: value
    };
    onUpdate(updatedConditions);
  };

  return (
    <div className="mb-6">
      <label className="block text-gray-700 text-sm font-bold mb-2">Conditions:</label>
      <div className="space-y-4">
        {conditions.map((condition, index) => (
          <div key={index} className="flex flex-wrap gap-2 items-center bg-gray-50 p-3 rounded-lg">
            <select
              value={condition.type}
              onChange={(e) => handleChange(index, 'type', e.target.value as Condition['type'])}
              className="px-3 py-2 border rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
              data-testid="conditionType"
            >
              {conditionTypes.map(opt => (
                <option key={opt.value} value={opt.value}>{opt.text}</option>
              ))}
            </select>
            
            <input
              type="number"
              value={condition.value}
              onChange={(e) => handleChange(index, 'value', e.target.value)}
              className="w-20 px-3 py-2 border rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
              data-testid="conditionValue"
              min="1"
            />

            {condition.type === 'countMatching' && (
              <input
                type="number"
                value={condition.count || ''}
                onChange={(e) => handleChange(index, 'count', e.target.value)}
                className="w-20 px-3 py-2 border rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                data-testid="conditionCount"
                min="1"
                placeholder="Count"
              />
            )}

            <button
              onClick={() => handleRemove(index)}
              className="px-2 py-1 bg-red-500 text-white rounded-lg hover:bg-red-600 focus:outline-none focus:ring-2 focus:ring-red-500"
            >
              Remove
            </button>
          </div>
        ))}
      </div>
      <button
        onClick={handleAdd}
        className="mt-2 px-4 py-2 bg-green-500 text-white rounded-lg hover:bg-green-600 focus:outline-none focus:ring-2 focus:ring-green-500"
      >
        Add Condition
      </button>
    </div>
  );
}
