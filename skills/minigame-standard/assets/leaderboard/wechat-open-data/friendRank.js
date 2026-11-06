function escapeValue(value) {
    return String(value === undefined || value === null ? '' : value)
        .replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

const MEDAL_IMAGES = [
    'open-data/render/image/rank-medal-gold.png',
    'open-data/render/image/rank-medal-silver.png',
    'open-data/render/image/rank-medal-bronze.png',
];

function row(item, rank, pinned) {
    const unranked = pinned && item.unranked;
    const rowClass = pinned ? 'selfItem' : (rank === 1 ? 'firstItem' : 'listItem');
    const name = pinned ? '我' : (item.nickname || '微信好友');
    let rankContent;
    if (unranked) {
        rankContent = '<text class="rankUnranked" value="未上榜"></text>';
    } else if (rank >= 1 && rank <= 3) {
        rankContent = '<image class="rankMedal" src="' + MEDAL_IMAGES[rank - 1] + '"></image>';
    } else {
        rankContent = '<text class="rankNumber" value="' + rank + '"></text>';
    }
    const score = unranked ? '—' : ('★ ' + escapeValue(item.score || 0));
    return '<view class="' + rowClass + '">' +
        '<view class="rankSlot">' + rankContent + '</view>' +
        '<view class="avatarSlot">' +
        '<image class="rankAvatarPlaceholder" src="open-data/render/image/rank-avatar-placeholder.png"></image>' +
        '<image class="rankAvatar" src="' + escapeValue(item.avatarUrl) + '"></image>' +
        '</view>' +
        '<text class="rankName" value="' + escapeValue(name) + '"></text>' +
        '<text class="rankScore" value="' + score + '"></text>' +
        '</view>';
}

export default function tplFunc(it) {
    const data = it.data || [];
    let selfItem = null;
    let selfRank = 0;
    let out = '<view class="container" id="main">' +
        '<view class="header">' +
        '<text class="headerRank" value="名次"></text>' +
        '<text class="headerName" value="昵称"></text>' +
        '<text class="headerScore" value="通关数"></text>' +
        '</view><scrollview class="list" scrollY="true">';

    if (!data.length) {
        out += '<view class="emptyState">' +
            '<text class="emptyText" value="暂无好友排行数据"></text>' +
            '<text class="emptyHint" value="完成关卡后再来看看"></text>' +
            '</view>';
    }
    data.forEach((item, index) => {
        if (item.isSelf) {
            selfItem = item;
            selfRank = index + 1;
        } else {
            out += row(item, index + 1, false);
        }
    });
    out += '</scrollview>';
    out += selfItem ? row(selfItem, selfRank, true) : row({ unranked: true, avatarUrl: '' }, 0, true);
    out += '</view>';
    return out;
}
