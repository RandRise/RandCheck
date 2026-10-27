type Props = {
  showQuotations: boolean;
  onShowQuotations: (show: boolean) => void;
};

export function LibraryFilter({ showQuotations, onShowQuotations }: Props) {
  return <div className="library-filter">
    <label><input type="checkbox" checked={showQuotations}
      onChange={event => onShowQuotations(event.target.checked)} />
      <span>Show earlier quotations</span></label>
  </div>;
}
