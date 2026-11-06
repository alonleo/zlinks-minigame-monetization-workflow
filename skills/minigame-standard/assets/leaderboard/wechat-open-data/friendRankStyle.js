export default function getStyle(data) {
    const width = data.width;
    const height = data.height;
    const headerHeight = Math.ceil(height * 0.095);
    const pinnedHeight = Math.ceil(height * 0.15);
    const rowHeight = Math.ceil(height * 0.145);
    const rowWidth = Math.ceil(width * 0.96);
    const listHeight = height - headerHeight - pinnedHeight - height * 0.035;
    const avatarSize = rowHeight * 0.63;
    const rowStyle = (backgroundColor, borderColor, itemHeight, marginTop) => ({
        position: 'relative', width: rowWidth, height: itemHeight,
        marginLeft: width * 0.02, marginTop,
        flexDirection: 'row', alignItems: 'center',
        backgroundColor, borderRadius: 16, borderWidth: 2, borderColor,
    });
    return {
        container: { width, height, backgroundColor: '#edf9ff', borderRadius: 18 },
        header: { width, height: headerHeight, flexDirection: 'row', alignItems: 'center' },
        headerRank: { width: width * 0.16, height: headerHeight, lineHeight: headerHeight, textAlign: 'center', fontSize: width * 0.042, color: '#173b58' },
        headerName: { width: width * 0.54, height: headerHeight, lineHeight: headerHeight, textAlign: 'center', fontSize: width * 0.042, color: '#173b58' },
        headerScore: { width: width * 0.27, height: headerHeight, lineHeight: headerHeight, textAlign: 'center', fontSize: width * 0.042, color: '#173b58' },
        list: { width, height: listHeight },
        listItem: rowStyle('#f8fcff', '#b7dceb', rowHeight, height * 0.012),
        firstItem: rowStyle('#fff1a8', '#f3cf45', rowHeight, height * 0.012),
        selfItem: rowStyle('#cfeeff', '#55bde8', pinnedHeight * 0.82, height * 0.018),
        rankSlot: { width: width * 0.13, height: rowHeight, justifyContent: 'center', alignItems: 'center' },
        rankNumber: { width: width * 0.13, height: rowHeight, lineHeight: rowHeight, textAlign: 'center', fontSize: width * 0.052, color: '#123a5c' },
        rankUnranked: { width: width * 0.13, height: rowHeight, lineHeight: rowHeight, textAlign: 'center', fontSize: width * 0.03, color: '#2675a3' },
        rankMedal: { width: rowHeight * 0.66, height: rowHeight * 0.66 },
        avatarSlot: { position: 'relative', width: avatarSize, height: avatarSize, marginLeft: width * 0.015 },
        rankAvatarPlaceholder: { position: 'absolute', left: 0, top: 0, width: avatarSize, height: avatarSize, borderRadius: avatarSize * 0.5 },
        rankAvatar: { position: 'absolute', left: 0, top: 0, width: avatarSize, height: avatarSize, borderRadius: avatarSize * 0.5 },
        rankName: { width: width * 0.43, height: rowHeight, lineHeight: rowHeight, marginLeft: width * 0.025, fontSize: width * 0.045, textOverflow: 'ellipsis', whiteSpace: 'nowrap', color: '#123a5c' },
        rankScore: { width: width * 0.22, height: rowHeight, lineHeight: rowHeight, textAlign: 'center', fontSize: width * 0.047, color: '#123a5c' },
        emptyState: { width, height: listHeight, justifyContent: 'center', alignItems: 'center' },
        emptyText: { width, height: rowHeight * 0.72, lineHeight: rowHeight * 0.72, textAlign: 'center', fontSize: width * 0.052, color: '#2675a3' },
        emptyHint: { width, height: rowHeight * 0.58, lineHeight: rowHeight * 0.58, textAlign: 'center', fontSize: width * 0.036, color: '#6a9fbd' },
    };
}
